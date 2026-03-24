
export {};

// --- Global Interactivity ---

declare global {
    interface Window { Alpine: any; }
}

document.addEventListener('alpine:init', () => {
    // Global store for UI state
    if (window.Alpine) {
        window.Alpine.store('blog', {
            isLoading: false,
            setLoading(val: boolean) { this.isLoading = val; }
        });
    }
});

// HTMX Global Listeners
document.body.addEventListener('htmx:beforeRequest', (evt: any) => {
    if ((evt as any).detail.target?.id === 'load-more-container') {
        window.Alpine?.store('blog')?.setLoading(true);
    }
});

document.body.addEventListener('htmx:afterRequest', (evt: any) => {
    if ((evt as any).detail.target?.id === 'load-more-container') {
        window.Alpine?.store('blog')?.setLoading(false);
    }
});