// Markdown Enhancement JS - Syntax highlighting and copy functionality

window.markdownEnhance = {
    // Apply syntax highlighting to all code blocks
    highlightAll: function () {
        if (typeof Prism !== 'undefined') {
            Prism.highlightAll();
        }
    },

    // Highlight a specific element
    highlightElement: function (element) {
        if (typeof Prism !== 'undefined' && element) {
            Prism.highlightElement(element);
        }
    },

    // Add copy buttons to all code blocks
    addCopyButtons: function () {
        document.querySelectorAll('.markdown-content pre').forEach(function (pre) {
            // Skip if already has a copy button
            if (pre.querySelector('.code-copy-btn')) return;

            // Get the code element
            const code = pre.querySelector('code');
            if (!code) return;

            // Create wrapper for positioning
            pre.style.position = 'relative';

            // Get language from class
            const langClass = Array.from(code.classList).find(c => c.startsWith('language-'));
            const lang = langClass ? langClass.replace('language-', '') : '';

            // Create header with language label and copy button
            const header = document.createElement('div');
            header.className = 'code-header';
            header.innerHTML = `
                <span class="code-lang">${lang || 'code'}</span>
                <button class="code-copy-btn" title="Copy code">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                        <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                    </svg>
                    <span class="copy-text">Copy</span>
                </button>
            `;

            pre.insertBefore(header, pre.firstChild);

            // Add click handler for copy button
            const copyBtn = header.querySelector('.code-copy-btn');
            copyBtn.addEventListener('click', function () {
                const text = code.textContent;
                navigator.clipboard.writeText(text).then(function () {
                    copyBtn.querySelector('.copy-text').textContent = 'Copied!';
                    copyBtn.classList.add('copied');
                    setTimeout(function () {
                        copyBtn.querySelector('.copy-text').textContent = 'Copy';
                        copyBtn.classList.remove('copied');
                    }, 2000);
                }).catch(function (err) {
                    console.error('Failed to copy:', err);
                });
            });
        });
    },

    // Initialize all enhancements
    init: function () {
        this.highlightAll();
        this.addCopyButtons();
    }
};

// Auto-configure Prism autoloader path
if (typeof Prism !== 'undefined' && Prism.plugins && Prism.plugins.autoloader) {
    Prism.plugins.autoloader.languages_path = 'https://cdnjs.cloudflare.com/ajax/libs/prism/1.29.0/components/';
}
