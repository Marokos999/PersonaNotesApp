// Applies the saved (or OS-preferred) theme as early as possible; also used by the sidebar toggle.
(function () {
    function preferred() {
        try {
            const saved = localStorage.getItem('theme');
            if (saved === 'dark' || saved === 'light') return saved;
        } catch (e) { /* storage unavailable */ }
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    document.documentElement.dataset.theme = preferred();

    window.themeInterop = {
        get: () => document.documentElement.dataset.theme,
        toggle: () => {
            const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
            document.documentElement.dataset.theme = next;
            try { localStorage.setItem('theme', next); } catch (e) { /* ignore */ }
            return next;
        }
    };
})();
