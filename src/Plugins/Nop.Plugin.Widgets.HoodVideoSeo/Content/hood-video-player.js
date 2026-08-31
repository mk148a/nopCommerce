(() => {
    if (window.HoodVideoSeoPlayer) return;
    window.HoodVideoSeoPlayer = true;

    const closeModal = modal => {
        modal.remove();
        document.body.classList.remove('hood-video-modal-open');
    };

    document.addEventListener('click', event => {
        const card = event.target.closest('a.hood-product-video-card[data-youtube-id]');
        if (!card || event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey)
            return;

        const youtubeId = card.dataset.youtubeId;
        if (!/^[A-Za-z0-9_-]{11}$/.test(youtubeId || ''))
            return;

        event.preventDefault();

        const modal = document.createElement('div');
        modal.className = 'hood-video-modal';
        modal.setAttribute('role', 'presentation');

        const dialog = document.createElement('section');
        dialog.className = 'hood-video-modal__dialog';
        dialog.setAttribute('role', 'dialog');
        dialog.setAttribute('aria-modal', 'true');
        dialog.setAttribute('aria-label', card.getAttribute('aria-label') || 'Product video');

        const close = document.createElement('button');
        close.className = 'hood-video-modal__close';
        close.type = 'button';
        close.textContent = '×';
        close.setAttribute('aria-label', 'Close video');

        const frame = document.createElement('iframe');
        frame.title = card.getAttribute('aria-label') || 'Product video';
        frame.loading = 'eager';
        frame.referrerPolicy = 'strict-origin-when-cross-origin';
        frame.allow = 'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share';
        frame.allowFullscreen = true;
        const parameters = new URLSearchParams({
            // Do not force autoplay: YouTube may classify an immediately
            // playing cross-origin embed as automated traffic and show its
            // sign-in/bot interstitial. Playback remains available through
            // the native player controls after the user explicitly clicks.
            autoplay: '0',
            enablejsapi: '1',
            rel: '0',
            playsinline: '1',
            origin: window.location.origin,
            widget_referrer: window.location.href
        });
        frame.src = `https://www.youtube-nocookie.com/embed/${youtubeId}?${parameters}`;

        const onKeyDown = item => {
            if (item.key !== 'Escape') return;
            document.removeEventListener('keydown', onKeyDown);
            closeModal(modal);
        };
        const dismiss = () => {
            document.removeEventListener('keydown', onKeyDown);
            closeModal(modal);
        };

        close.addEventListener('click', dismiss);
        modal.addEventListener('click', item => {
            if (item.target === modal) dismiss();
        });
        document.addEventListener('keydown', onKeyDown);

        dialog.append(close, frame);
        modal.append(dialog);
        document.body.append(modal);
        document.body.classList.add('hood-video-modal-open');
        close.focus();
    });
})();
