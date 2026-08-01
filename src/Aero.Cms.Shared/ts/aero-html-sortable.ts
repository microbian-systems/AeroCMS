type DotNetCallback = {
  invokeMethodAsync(methodIdentifier: string, ...args: unknown[]): Promise<unknown>;
};

type Placement = 'before' | 'after' | 'inside';

type DropProposal = {
  element: HTMLElement;
  targetNodeId: string;
  placement: Placement;
};

type ActiveDrag = {
  pointerId: number;
  startX: number;
  startY: number;
  source: HTMLElement;
  dragging: boolean;
  proposal: DropProposal | null;
};

type ActivePaletteDrag = {
  pointerId: number;
  startX: number;
  startY: number;
  source: HTMLElement;
  itemKind: string;
  itemValue: string;
  ready: boolean;
  dragging: boolean;
  proposal: DropProposal | null;
  preparation: Promise<boolean>;
};

type SortableInstance = {
  dispose(): void;
};

const instances = new Map<string, SortableInstance>();
const DROP_CLASSES = [
  'aero-sort-drop-before',
  'aero-sort-drop-after',
  'aero-sort-drop-inside',
] as const;
const DRAG_THRESHOLD = 5;
const ON_EDITOR_COMMAND_REQUESTED = 'OnEditorCommandRequested';

export function initialize(
  surface: HTMLElement,
  selectionToolbar: HTMLElement,
  dragHandle: HTMLButtonElement,
  dotNetCallback: DotNetCallback,
): string {
  let activeDrag: ActiveDrag | null = null;
  let activePaletteDrag: ActivePaletteDrag | null = null;
  let suppressClickUntil = 0;
  let suppressedPaletteSource: HTMLElement | null = null;
  let suppressPaletteClickUntil = 0;
  let positionFrame: number | null = null;

  const isEnabled = (): boolean => surface.dataset.aeroSortableEnabled === 'true';

  const selectedNode = (): HTMLElement | null =>
    surface.querySelector<HTMLElement>(
      '.aero-editor-node-selected[data-aero-sortable-node="true"]',
    );

  const scheduleHandlePosition = (): void => {
    if (positionFrame !== null) {
      cancelAnimationFrame(positionFrame);
    }

    positionFrame = requestAnimationFrame(() => {
      positionFrame = null;
      const selected = selectedNode();
      if (!isEnabled() || !selected) {
        selectionToolbar.classList.remove('is-visible');
        return;
      }

      const surfaceRect = surface.getBoundingClientRect();
      const selectedRect = selected.getBoundingClientRect();
      selectionToolbar.style.left = `${selectedRect.left - surfaceRect.left + surface.scrollLeft + 4}px`;
      selectionToolbar.style.top = `${selectedRect.top - surfaceRect.top + surface.scrollTop + 4}px`;
      selectionToolbar.classList.add('is-visible');
    });
  };

  const clearDropProposal = (): void => {
    surface.classList.remove(...DROP_CLASSES);
    surface.querySelectorAll<HTMLElement>(`.${DROP_CLASSES.join(',.')}`).forEach((element) => {
      element.classList.remove(...DROP_CLASSES);
    });
  };

  const showDropProposal = (proposal: DropProposal | null): void => {
    clearDropProposal();
    if (proposal) {
      proposal.element.classList.add(`aero-sort-drop-${proposal.placement}`);
    }
  };

  const closestNode = (target: Element | null): HTMLElement | null => {
    const node = target?.closest<HTMLElement>('[data-aero-node-id]') ?? null;
    return node && surface.contains(node) ? node : null;
  };

  const proposeDrop = (
    clientX: number,
    clientY: number,
    source: HTMLElement | null,
  ): DropProposal | null => {
    const pointedElement = document.elementFromPoint(clientX, clientY);
    const target = closestNode(pointedElement);

    if (target) {
      if (source && (target === source || source.contains(target))) {
        return null;
      }

      const targetNodeId = target.dataset.aeroNodeId;
      if (!targetNodeId) {
        return null;
      }

      const rect = target.getBoundingClientRect();
      const verticalRatio = rect.height > 0
        ? Math.min(1, Math.max(0, (clientY - rect.top) / rect.height))
        : 0.5;
      let placement: Placement;
      const canAcceptAsSibling = target.dataset.aeroCanAcceptSelectedAsSibling === 'true';
      const canAcceptInside = target.dataset.aeroCanAcceptSelectedInside === 'true';

      if (verticalRatio < 0.25 && canAcceptAsSibling) {
        placement = 'before';
      } else if (verticalRatio > 0.75 && canAcceptAsSibling) {
        placement = 'after';
      } else if (canAcceptInside) {
        placement = 'inside';
      } else if (canAcceptAsSibling) {
        placement = verticalRatio < 0.5 ? 'before' : 'after';
      } else {
        return null;
      }

      return { element: target, targetNodeId, placement };
    }

    const surfaceRect = surface.getBoundingClientRect();
    const isInsideSurface = clientX >= surfaceRect.left
      && clientX <= surfaceRect.right
      && clientY >= surfaceRect.top
      && clientY <= surfaceRect.bottom;
    const rootNodeId = surface.dataset.aeroRootNodeId;

    return isInsideSurface
      && rootNodeId
      && surface.dataset.aeroCanAcceptSelectedInside === 'true'
      ? { element: surface, targetNodeId: rootNodeId, placement: 'inside' }
      : null;
  };

  const beginDragging = (drag: ActiveDrag): void => {
    drag.dragging = true;
    surface.classList.add('aero-sort-active');
    drag.source.classList.add('aero-sort-source');
    dragHandle.classList.add('is-dragging');
  };

  const cleanupDrag = (): void => {
    if (!activeDrag) {
      return;
    }

    if (dragHandle.hasPointerCapture(activeDrag.pointerId)) {
      dragHandle.releasePointerCapture(activeDrag.pointerId);
    }

    activeDrag.source.classList.remove('aero-sort-source');
    activeDrag = null;
    surface.classList.remove('aero-sort-active');
    dragHandle.classList.remove('is-dragging');
    clearDropProposal();
    scheduleHandlePosition();
  };

  const requestMove = (
    source: HTMLElement,
    target: HTMLElement,
    placement: Placement,
  ): void => {
    const sourceId = source.dataset.aeroNodeId;
    const targetId = target.dataset.aeroNodeId;
    if (!sourceId || !targetId || sourceId === targetId) {
      return;
    }

    void dotNetCallback
      .invokeMethodAsync('OnSortMoveRequested', sourceId, targetId, placement)
      .catch((error: unknown) => console.error('Aero sortable move failed.', error));
  };

  const onPointerDown = (event: PointerEvent): void => {
    if (!isEnabled() || event.button !== 0) {
      return;
    }

    const source = selectedNode();
    if (!source) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    activeDrag = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      source,
      dragging: false,
      proposal: null,
    };
    dragHandle.setPointerCapture(event.pointerId);
  };

  const onPointerMove = (event: PointerEvent): void => {
    if (!activeDrag || activeDrag.pointerId !== event.pointerId) {
      return;
    }

    const distance = Math.hypot(
      event.clientX - activeDrag.startX,
      event.clientY - activeDrag.startY,
    );
    if (!activeDrag.dragging && distance < DRAG_THRESHOLD) {
      return;
    }

    event.preventDefault();
    if (!activeDrag.dragging) {
      beginDragging(activeDrag);
    }

    activeDrag.proposal = proposeDrop(event.clientX, event.clientY, activeDrag.source);
    showDropProposal(activeDrag.proposal);
  };

  const onPointerUp = (event: PointerEvent): void => {
    if (!activeDrag || activeDrag.pointerId !== event.pointerId) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    const completedDrag = activeDrag;
    if (completedDrag.dragging && completedDrag.proposal) {
      const sourceId = completedDrag.source.dataset.aeroNodeId;
      suppressClickUntil = performance.now() + 300;
      if (sourceId) {
        void dotNetCallback
          .invokeMethodAsync(
            'OnSortMoveRequested',
            sourceId,
            completedDrag.proposal.targetNodeId,
            completedDrag.proposal.placement,
          )
          .catch((error: unknown) => console.error('Aero sortable move failed.', error));
      }
    }

    cleanupDrag();
  };

  const onPointerCancel = (event: PointerEvent): void => {
    if (activeDrag?.pointerId === event.pointerId) {
      cleanupDrag();
    }
  };

  const endPalettePreparation = (preparation: Promise<boolean>): void => {
    void preparation
      .finally(() => dotNetCallback.invokeMethodAsync('OnPaletteDragEnded'))
      .catch((error: unknown) => console.error('Aero palette drag cleanup failed.', error));
  };

  const cleanupPaletteDrag = (notifyEnded: boolean): void => {
    if (!activePaletteDrag) {
      return;
    }

    const completedDrag = activePaletteDrag;
    completedDrag.source.classList.remove('aero-palette-drag-source');
    activePaletteDrag = null;
    surface.classList.remove('aero-sort-active');
    clearDropProposal();
    scheduleHandlePosition();

    if (notifyEnded) {
      endPalettePreparation(completedDrag.preparation);
    }
  };

  const onDocumentPointerDown = (event: PointerEvent): void => {
    if (!isEnabled() || event.button !== 0 || !(event.target instanceof Element)) {
      return;
    }

    const source = event.target.closest<HTMLElement>(
      '[data-aero-palette-kind][data-aero-palette-value]',
    );
    const itemKind = source?.dataset.aeroPaletteKind;
    const itemValue = source?.dataset.aeroPaletteValue;
    if (!source || !itemKind || !itemValue) {
      return;
    }

    const preparation = dotNetCallback
      .invokeMethodAsync('OnPaletteDragStarted', itemKind, itemValue)
      .then((allowed: unknown) => allowed === true);
    const paletteDrag: ActivePaletteDrag = {
      pointerId: event.pointerId,
      startX: event.clientX,
      startY: event.clientY,
      source,
      itemKind,
      itemValue,
      ready: false,
      dragging: false,
      proposal: null,
      preparation,
    };
    activePaletteDrag = paletteDrag;

    void preparation
      .then((allowed) => {
        if (activePaletteDrag !== paletteDrag) {
          return;
        }

        if (!allowed) {
          cleanupPaletteDrag(false);
          return;
        }

        paletteDrag.ready = true;
      })
      .catch((error: unknown) => {
        if (activePaletteDrag === paletteDrag) {
          cleanupPaletteDrag(true);
        }

        console.error('Aero palette drag preparation failed.', error);
      });
  };

  const onDocumentPointerMove = (event: PointerEvent): void => {
    if (!activePaletteDrag
      || activePaletteDrag.pointerId !== event.pointerId
      || !activePaletteDrag.ready) {
      return;
    }

    const distance = Math.hypot(
      event.clientX - activePaletteDrag.startX,
      event.clientY - activePaletteDrag.startY,
    );
    if (!activePaletteDrag.dragging && distance < DRAG_THRESHOLD) {
      return;
    }

    event.preventDefault();
    if (!activePaletteDrag.dragging) {
      activePaletteDrag.dragging = true;
      activePaletteDrag.source.classList.add('aero-palette-drag-source');
      surface.classList.add('aero-sort-active');
      selectionToolbar.classList.remove('is-visible');
    }

    activePaletteDrag.proposal = proposeDrop(event.clientX, event.clientY, null);
    showDropProposal(activePaletteDrag.proposal);
  };

  const onDocumentPointerUp = (event: PointerEvent): void => {
    if (!activePaletteDrag || activePaletteDrag.pointerId !== event.pointerId) {
      return;
    }

    const completedDrag = activePaletteDrag;
    const distance = Math.hypot(
      event.clientX - completedDrag.startX,
      event.clientY - completedDrag.startY,
    );

    // The Blazor content-policy callback can finish after a quick pointer
    // gesture. Preserve the validated release location instead of dropping the
    // gesture merely because the callback was still in flight at pointer-up.
    if (distance >= DRAG_THRESHOLD
      && (!completedDrag.ready || completedDrag.proposal === null)) {
      event.preventDefault();
      event.stopPropagation();
      suppressedPaletteSource = completedDrag.source;
      suppressPaletteClickUntil = performance.now() + 300;
      const releaseX = event.clientX;
      const releaseY = event.clientY;
      cleanupPaletteDrag(false);

      void (async (): Promise<void> => {
        let inserted = false;
        try {
          if (await completedDrag.preparation) {
            // The interop result and the render batch are separate browser
            // turns. Allow the validated drop-zone attributes to reach the DOM
            // before resolving the preserved release point.
            await new Promise<void>((resolve) => requestAnimationFrame(
              () => requestAnimationFrame(() => resolve()),
            ));
            const proposal = proposeDrop(releaseX, releaseY, null);
            if (proposal) {
              await dotNetCallback.invokeMethodAsync(
                'OnPaletteInsertRequested',
                completedDrag.itemKind,
                completedDrag.itemValue,
                proposal.targetNodeId,
                proposal.placement,
              );
              inserted = true;
            }
          }
        } catch (error: unknown) {
          console.error('Aero palette insertion failed.', error);
        } finally {
          if (!inserted) {
            await dotNetCallback.invokeMethodAsync('OnPaletteDragEnded');
          }
        }
      })().catch((error: unknown) => console.error('Aero palette drag cleanup failed.', error));
      return;
    }

    const shouldInsert = completedDrag.dragging && completedDrag.proposal !== null;
    if (shouldInsert) {
      event.preventDefault();
      event.stopPropagation();
      suppressedPaletteSource = completedDrag.source;
      suppressPaletteClickUntil = performance.now() + 300;
      void dotNetCallback
        .invokeMethodAsync(
          'OnPaletteInsertRequested',
          completedDrag.itemKind,
          completedDrag.itemValue,
          completedDrag.proposal!.targetNodeId,
          completedDrag.proposal!.placement,
        )
        .catch((error: unknown) => console.error('Aero palette insertion failed.', error));
    }

    cleanupPaletteDrag(!shouldInsert);
  };

  const onDocumentPointerCancel = (event: PointerEvent): void => {
    if (activePaletteDrag?.pointerId === event.pointerId) {
      cleanupPaletteDrag(true);
    }
  };

  const onDocumentClick = (event: MouseEvent): void => {
    if (performance.now() >= suppressPaletteClickUntil
      || !(event.target instanceof Element)
      || !suppressedPaletteSource?.contains(event.target)) {
      return;
    }

    event.preventDefault();
    event.stopImmediatePropagation();
    suppressedPaletteSource = null;
  };

  const adjacentSortableNode = (
    source: HTMLElement,
    direction: 'previous' | 'next',
  ): HTMLElement | null => {
    let sibling = direction === 'previous'
      ? source.previousElementSibling
      : source.nextElementSibling;
    while (sibling) {
      if (sibling instanceof HTMLElement && sibling.dataset.aeroSortableNode === 'true') {
        return sibling;
      }

      sibling = direction === 'previous'
        ? sibling.previousElementSibling
        : sibling.nextElementSibling;
    }

    return null;
  };

  const onKeyDown = (event: KeyboardEvent): void => {
    if (!isEnabled()) {
      return;
    }

    const source = selectedNode();
    if (!source) {
      return;
    }

    let target: HTMLElement | null = null;
    let placement: Placement = 'before';
    switch (event.key) {
      case 'ArrowUp':
        target = adjacentSortableNode(source, 'previous');
        break;
      case 'ArrowDown':
        target = adjacentSortableNode(source, 'next');
        placement = 'after';
        break;
      case 'ArrowRight':
        target = adjacentSortableNode(source, 'previous');
        placement = 'inside';
        break;
      case 'ArrowLeft':
        target = closestNode(source.parentElement);
        placement = 'after';
        break;
      default:
        return;
    }

    if (target) {
      event.preventDefault();
      event.stopPropagation();
      requestMove(source, target, placement);
    }
  };

  const onHandleClick = (event: MouseEvent): void => {
    event.preventDefault();
    event.stopPropagation();
  };

  const onSurfaceClick = (event: MouseEvent): void => {
    if (performance.now() < suppressClickUntil) {
      event.preventDefault();
      event.stopImmediatePropagation();
    }
  };

  const isEditableTarget = (target: EventTarget | null): boolean =>
    target instanceof Element
    && target.closest('input, textarea, select, [contenteditable="true"]') !== null;

  const restoreCommandFocus = (): void => {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      scheduleHandlePosition();
      if (selectedNode()) {
        dragHandle.focus({ preventScroll: true });
      } else {
        surface.focus({ preventScroll: true });
      }
    }));
  };

  const requestEditorCommand = async (
    command: 'undo' | 'redo' | 'duplicate' | 'delete',
  ): Promise<void> => {
    try {
      await dotNetCallback.invokeMethodAsync(ON_EDITOR_COMMAND_REQUESTED, command);
      restoreCommandFocus();
    } catch (error: unknown) {
      console.error('Aero editor command failed.', error);
    }
  };

  const onDocumentKeyDown = (event: KeyboardEvent): void => {
    if (event.key === 'Escape' && activeDrag) {
      event.preventDefault();
      cleanupDrag();
    } else if (event.key === 'Escape' && activePaletteDrag) {
      event.preventDefault();
      cleanupPaletteDrag(true);
    }

    if (!isEnabled() || !selectedNode() || isEditableTarget(event.target)) {
      return;
    }

    let command: 'undo' | 'redo' | 'duplicate' | 'delete' | null = null;
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
      command = event.shiftKey ? 'redo' : 'undo';
    } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'y') {
      command = 'redo';
    } else if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'd') {
      command = 'duplicate';
    } else if (event.key === 'Delete' || event.key === 'Backspace') {
      command = 'delete';
    }

    if (!command) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();
    void requestEditorCommand(command);
  };

  const observer = new MutationObserver(scheduleHandlePosition);
  observer.observe(surface, {
    attributes: true,
    attributeFilter: ['class', 'data-aero-sortable-enabled'],
    childList: true,
    subtree: true,
  });

  dragHandle.addEventListener('pointerdown', onPointerDown);
  dragHandle.addEventListener('pointermove', onPointerMove);
  dragHandle.addEventListener('pointerup', onPointerUp);
  dragHandle.addEventListener('pointercancel', onPointerCancel);
  dragHandle.addEventListener('keydown', onKeyDown);
  dragHandle.addEventListener('click', onHandleClick);
  surface.addEventListener('click', onSurfaceClick, true);
  document.addEventListener('keydown', onDocumentKeyDown);
  document.addEventListener('pointerdown', onDocumentPointerDown);
  document.addEventListener('pointermove', onDocumentPointerMove);
  document.addEventListener('pointerup', onDocumentPointerUp);
  document.addEventListener('pointercancel', onDocumentPointerCancel);
  document.addEventListener('click', onDocumentClick, true);
  window.addEventListener('resize', scheduleHandlePosition);
  surface.dataset.aeroSortableInitialized = 'true';
  scheduleHandlePosition();

  const handle = crypto.randomUUID();
  instances.set(handle, {
    dispose(): void {
      cleanupDrag();
      cleanupPaletteDrag(true);
      observer.disconnect();
      if (positionFrame !== null) {
        cancelAnimationFrame(positionFrame);
      }

      dragHandle.removeEventListener('pointerdown', onPointerDown);
      dragHandle.removeEventListener('pointermove', onPointerMove);
      dragHandle.removeEventListener('pointerup', onPointerUp);
      dragHandle.removeEventListener('pointercancel', onPointerCancel);
      dragHandle.removeEventListener('keydown', onKeyDown);
      dragHandle.removeEventListener('click', onHandleClick);
      surface.removeEventListener('click', onSurfaceClick, true);
      document.removeEventListener('keydown', onDocumentKeyDown);
      document.removeEventListener('pointerdown', onDocumentPointerDown);
      document.removeEventListener('pointermove', onDocumentPointerMove);
      document.removeEventListener('pointerup', onDocumentPointerUp);
      document.removeEventListener('pointercancel', onDocumentPointerCancel);
      document.removeEventListener('click', onDocumentClick, true);
      window.removeEventListener('resize', scheduleHandlePosition);
      delete surface.dataset.aeroSortableInitialized;
    },
  });

  return handle;
}

export function dispose(handle: string): void {
  instances.get(handle)?.dispose();
  instances.delete(handle);
}
