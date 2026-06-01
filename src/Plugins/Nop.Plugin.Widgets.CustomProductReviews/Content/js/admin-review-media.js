(function () {
    if (window.hoodCustomReviewMediaLoaded) return;
    window.hoodCustomReviewMediaLoaded = true;

    var maxItems = window.hoodCustomReviewMediaMaxItems || 6;

    function findReviewRows() {
        var rows = [];
        document.querySelectorAll('table tbody tr').forEach(function (row) {
            var link = row.querySelector('a[href*="/ProductReview/Edit/"]');
            if (!link) link = row.querySelector('a[href*="ProductReview/Edit"]');
            if (!link) return;

            var href = link.getAttribute('href') || '';
            var match = href.match(/ProductReview\/Edit\/(\d+)/i);
            if (!match) return;

            var id = parseInt(match[1], 10);
            if (!id || row.getAttribute('data-hood-review-media') === '1') return;

            rows.push({ row: row, id: id });
        });
        return rows;
    }

    function getTargetCell(row) {
        var headers = Array.prototype.slice.call(document.querySelectorAll('table thead th'))
            .map(function (th) { return (th.textContent || '').trim().toLowerCase(); });

        var reviewTextIndex = headers.findIndex(function (text) {
            return text.indexOf('review text') >= 0 || text.indexOf('message') >= 0;
        });

        var cells = row.querySelectorAll('td');
        if (reviewTextIndex >= 0 && cells[reviewTextIndex]) return cells[reviewTextIndex];
        return cells.length >= 4 ? cells[3] : row.lastElementChild;
    }

    function renderMedia(row, item) {
        var target = getTargetCell(row);
        if (!target) return;

        var box = document.createElement('div');
        box.className = 'hood-admin-review-media';

        if (item && item.media && item.media.length) {
            item.media.slice(0, maxItems).forEach(function (media) {
                if (media.type === 'image') {
                    var a = document.createElement('a');
                    a.href = media.url || media.thumbUrl;
                    a.target = '_blank';
                    a.rel = 'noopener noreferrer';

                    var img = document.createElement('img');
                    img.src = media.thumbUrl || media.url;
                    img.loading = 'lazy';

                    a.appendChild(img);
                    box.appendChild(a);
                }

                if (media.type === 'video') {
                    var video = document.createElement('video');
                    video.src = media.url;
                    video.controls = true;
                    video.preload = 'metadata';
                    box.appendChild(video);
                }
            });

            if (item.count > maxItems) {
                var badge = document.createElement('span');
                badge.className = 'hood-admin-review-media__badge';
                badge.textContent = '+' + (item.count - maxItems);
                box.appendChild(badge);
            }

            target.appendChild(box);
        }

        row.setAttribute('data-hood-review-media', '1');
    }

    function load() {
        var rows = findReviewRows();
        if (!rows.length) return;

        var ids = rows.map(function (item) { return item.id; }).join(',');
        fetch('/Admin/CustomProductReviewsAdmin/ReviewMediaSummary?productReviewIds=' + encodeURIComponent(ids), { credentials: 'same-origin' })
            .then(function (response) { return response.json(); })
            .then(function (data) {
                if (!data || !data.success) return;

                var map = {};
                (data.items || []).forEach(function (item) {
                    map[item.productReviewId] = item;
                });

                rows.forEach(function (rowItem) {
                    renderMedia(rowItem.row, map[rowItem.id]);
                });
            })
            .catch(function () { });
    }

    setInterval(load, 1200);
    if (document.readyState === 'loading')
        document.addEventListener('DOMContentLoaded', load);
    else
        load();
})();
