// Browser-side helpers for the Briefcase web UI, called from Blazor via JS interop.
window.briefcase = {
    // Syntax-highlights code blocks the server tagged with a "language-xxx" class (code files and
    // fenced blocks in Markdown). Blocks highlight.js has already done carry data-highlighted and
    // are skipped, so this is cheap to call after every render. Languages not in the vendored
    // bundle are left as plain text rather than auto-detected.
    highlight: function (container) {
        if (!container || !window.hljs) return;
        container.querySelectorAll('pre code[class*="language-"]').forEach(function (block) {
            if (block.dataset.highlighted) return;
            var match = /language-([\w+#-]+)/.exec(block.className);
            if (match && hljs.getLanguage(match[1])) hljs.highlightElement(block);
        });
    }
};
