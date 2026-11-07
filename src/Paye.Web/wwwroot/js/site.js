/**
 * Payé Digital - Modern Shell & Framework Bridge (site.js)
 * Strict CSP compliant: No inline scripts required
 */
(function () {
    'use strict';

    // 1. Dark Mode / Theme Switcher Persistence
    function initTheme() {
        const toggleBtn = document.getElementById('theme-toggle-btn');
        const savedTheme = localStorage.getItem('paye_theme_mode');
        const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
        const activeTheme = savedTheme || (prefersDark ? 'dark' : 'light');

        applyTheme(activeTheme);

        if (toggleBtn) {
            toggleBtn.addEventListener('click', function () {
                const current = document.documentElement.getAttribute('data-theme') || 'light';
                const next = current === 'dark' ? 'light' : 'dark';
                applyTheme(next);
                try {
                    localStorage.setItem('paye_theme_mode', next);
                } catch (e) {
                    console.warn('Storage unavailable', e);
                }
            });
        }
    }

    function applyTheme(theme) {
        document.documentElement.setAttribute('data-theme', theme);
        if (theme === 'dark') {
            document.documentElement.classList.add('dark');
        } else {
            document.documentElement.classList.remove('dark');
        }
    }

    // 2. Native Accessible <dialog> Modals and Drawers
    function initDialogs() {
        document.addEventListener('click', function (e) {
            const openTrigger = e.target.closest('[data-dialog-open]');
            if (openTrigger) {
                const dialogId = openTrigger.getAttribute('data-dialog-open');
                const dialog = document.getElementById(dialogId);
                if (dialog && typeof dialog.showModal === 'function') {
                    dialog.showModal();
                }
                return;
            }

            const closeTrigger = e.target.closest('[data-dialog-close]');
            if (closeTrigger) {
                const dialog = closeTrigger.closest('dialog');
                if (dialog && typeof dialog.close === 'function') {
                    dialog.close();
                }
                return;
            }

            // Close dialog when clicking directly on backdrop
            if (e.target.tagName === 'DIALOG' && e.target.hasAttribute('open')) {
                const rect = e.target.getBoundingClientRect();
                const isInDialog = (
                    rect.top <= e.clientY && e.clientY <= rect.top + rect.height &&
                    rect.left <= e.clientX && e.clientX <= rect.left + rect.width
                );
                if (!isInDialog) {
                    e.target.close();
                }
            }
        });
    }

    // 3. Antiforgery Header Integration for HTMX and Fetch
    function initAntiforgery() {
        const tokenMeta = document.querySelector('meta[name="csrf-token"]');
        const token = tokenMeta ? tokenMeta.getAttribute('content') : null;

        if (token && window.htmx) {
            document.addEventListener('htmx:configRequest', function (evt) {
                evt.detail.headers['X-CSRF-TOKEN'] = token;
            });
        }
    }

    // DOM Ready
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            initTheme();
            initDialogs();
            initAntiforgery();
        });
    } else {
        initTheme();
        initDialogs();
        initAntiforgery();
    }
})();
