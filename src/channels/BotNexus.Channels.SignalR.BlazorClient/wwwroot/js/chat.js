// BotNexus Blazor Client — Chat scroll helpers
window.chatScroll = {
    /**
     * Scrolls to bottom only if the user is already near the bottom (within threshold).
     * This preserves scroll position when the user has scrolled up to read history.
     */
    scrollToBottom: function (element) {
        if (!element) return;
        var threshold = 100;
        var isNearBottom = element.scrollHeight - element.scrollTop - element.clientHeight < threshold;
        if (isNearBottom) {
            element.scrollTop = element.scrollHeight;
        }
    },

    /** Force-scrolls to bottom regardless of current position. */
    forceScrollToBottom: function (element) {
        if (!element) return;
        element.scrollTop = element.scrollHeight;
    }
};
