document.addEventListener('alpine:init', () => {
    // Global store for UI state
    if (window.Alpine) {
        window.Alpine.store('blog', {
            isLoading: false,
            setLoading(val) { this.isLoading = val; }
        });
    }
});
// HTMX Global Listeners
document.body.addEventListener('htmx:beforeRequest', (evt) => {
    if (evt.detail.target?.id === 'posts-container') {
        window.Alpine?.store('blog')?.setLoading(true);
    }
});
document.body.addEventListener('htmx:afterRequest', (evt) => {
    if (evt.detail.target?.id === 'posts-container') {
        window.Alpine?.store('blog')?.setLoading(false);
    }
});
export {};
