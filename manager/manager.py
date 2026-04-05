#!/usr/bin/env python3

import docker
import os
import psycopg2
import signal
from os import environ
from sys import exit, stderr
from time import sleep

HEALTHCHECK_FILE = "/healthcheck/manager-ready"
WORKER_LABEL = "com.citusdata.role=Worker"
MASTER_PORT = 5432


def log(msg: str) -> None:
    print(msg, file=stderr, flush=True)


def connect_to_master():
    citus_host = environ.get("CITUS_HOST", "master")
    postgres_pass = environ.get("POSTGRES_PASSWORD", "")
    postgres_user = environ.get("POSTGRES_USER", "postgres")
    postgres_db = environ.get("POSTGRES_DB", postgres_user)

    conn = None
    while conn is None:
        try:
            conn = psycopg2.connect(
                dbname=postgres_db,
                user=postgres_user,
                host=citus_host,
                password=postgres_pass,
            )
        except psycopg2.OperationalError:
            log(f"Could not connect to {citus_host}, trying again in 1 second")
            sleep(1)
        except Exception as error:
            log(f"Unexpected error connecting to {citus_host}: {error}")
            raise

    conn.autocommit = True
    log(f"connected to {citus_host}")
    return conn


def node_exists(conn, host: str, port: int = MASTER_PORT) -> bool:
    with conn.cursor() as cur:
        cur.execute(
            """
            SELECT 1
            FROM pg_dist_node
            WHERE nodename = %s AND nodeport = %s
            LIMIT 1
            """,
            (host, port),
        )
        return cur.fetchone() is not None


def add_worker(conn, host: str, port: int = MASTER_PORT) -> None:
    try:
        if node_exists(conn, host, port):
            log(f"worker already registered: {host}:{port}")
            return

        with conn.cursor() as cur:
            log(f"adding {host}:{port}")
            cur.execute("SELECT master_add_node(%s, %s)", (host, port))
            result = cur.fetchone()
            log(f"added {host}:{port} -> {result}")
    except Exception as e:
        log(f"failed to add worker {host}:{port}: {e}")


def remove_worker(conn, host: str, port: int = MASTER_PORT) -> None:
    try:
        if not node_exists(conn, host, port):
            log(f"worker not registered, skipping remove: {host}:{port}")
            return

        with conn.cursor() as cur:
            log(f"removing {host}:{port}")
            cur.execute(
                """
                DELETE FROM pg_dist_placement
                WHERE groupid = (
                    SELECT groupid
                    FROM pg_dist_node
                    WHERE nodename = %s AND nodeport = %s
                    LIMIT 1
                )
                """,
                (host, port),
            )

            cur.execute("SELECT master_remove_node(%s, %s)", (host, port))
            result = cur.fetchone()
            log(f"removed {host}:{port} -> {result}")
    except Exception as e:
        log(f"failed to remove worker {host}:{port}: {e}")


def get_my_container(client: docker.DockerClient):
    """
    In Docker containers, HOSTNAME is usually the container ID.
    We try that first, then fall back to matching by name/hostname.
    """
    my_hostname = environ.get("HOSTNAME")
    if not my_hostname:
        raise RuntimeError("HOSTNAME environment variable is not set")

    try:
        return client.containers.get(my_hostname)
    except Exception as ex:
        log(f"primary container lookup failed for HOSTNAME={my_hostname}: {ex}")

    for c in client.containers.list(all=True):
        try:
            if c.name == my_hostname:
                return c
            if c.attrs.get("Config", {}).get("Hostname") == my_hostname:
                return c
            if c.id.startswith(my_hostname):
                return c
        except Exception:
            pass

    raise RuntimeError(f"Could not resolve current container for HOSTNAME={my_hostname}")


def get_compose_project(container) -> str:
    compose_project = container.labels.get("com.docker.compose.project")
    if not compose_project:
        raise RuntimeError("Could not determine compose project from container labels")
    return compose_project


def get_worker_name(container) -> str:
    name = container.name or ""
    if name.startswith("/"):
        name = name[1:]
    return name


def is_container_healthy(container) -> bool:
    try:
        container.reload()
        state = container.attrs.get("State", {})
        health = state.get("Health", {})
        status = health.get("Status")
        if status:
            return status == "healthy"
        return state.get("Status") == "running"
    except Exception as e:
        log(f"failed to inspect health for container {container.name}: {e}")
        return False


def reconcile_existing_workers(client: docker.DockerClient, conn, compose_project: str) -> None:
    log("reconciling existing worker containers...")

    filters = {
        "label": [
            f"com.docker.compose.project={compose_project}",
            WORKER_LABEL,
        ]
    }

    try:
        containers = client.containers.list(all=True, filters=filters)
    except Exception as e:
        log(f"failed to list worker containers for reconciliation: {e}")
        return

    if not containers:
        log("no worker containers found during reconciliation")
        return

    for container in containers:
        worker_name = get_worker_name(container)
        healthy = is_container_healthy(container)
        log(f"found worker {worker_name}, healthy={healthy}")

        if healthy:
            add_worker(conn, worker_name, MASTER_PORT)
        else:
            log(f"skipping unhealthy worker {worker_name}")


def docker_checker():
    client = docker.DockerClient(
        base_url="unix:///var/run/docker.sock",
        version="auto",
    )

    actions = {
        "health_status: healthy": add_worker,
        "destroy": remove_worker,
    }

    conn = connect_to_master()

    this_container = get_my_container(client)
    compose_project = get_compose_project(this_container)

    log(f"found compose project: {compose_project}")

    reconcile_existing_workers(client, conn, compose_project)

    filters = {
        "event": list(actions.keys()),
        "label": [
            f"com.docker.compose.project={compose_project}",
            WORKER_LABEL,
        ],
        "type": "container",
    }

    log("listening for events...")
    open(HEALTHCHECK_FILE, "a").close()

    for event in client.events(decode=True, filters=filters):
        try:
            actor_attrs = event.get("Actor", {}).get("Attributes", {})
            worker_name = actor_attrs.get("name", "")
            if worker_name.startswith("/"):
                worker_name = worker_name[1:]

            status = event.get("status") or event.get("Action")

            if not worker_name:
                log(f"received event without worker name: {event}")
                continue

            log(f"received event: status={status}, worker={worker_name}")

            action = actions.get(status)
            if action:
                action(conn, worker_name, MASTER_PORT)
            else:
                log(f"received unknown status '{status}' for {worker_name}")
        except Exception as e:
            log(f"error processing event: {e}. Event data: {event}")


def graceful_shutdown(sig, frame):
    log("shutting down...")
    exit(0)


def main():
    if os.path.exists(HEALTHCHECK_FILE):
        os.remove(HEALTHCHECK_FILE)

    signal.signal(signal.SIGTERM, graceful_shutdown)
    docker_checker()


if __name__ == "__main__":
    main()