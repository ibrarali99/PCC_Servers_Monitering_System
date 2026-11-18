// New-report form: photo previews, "all OK" shortcuts, client-side photo shrinking and AJAX submit.
(function () {
    const MAX_SIDE = 1600, QUALITY = 0.82;
    const form = document.getElementById('reportForm');
    const btn = document.getElementById('saveBtn');
    const msg = document.getElementById('saveMessage');

    const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    // ---- thumbnails ----
    document.querySelectorAll('.photo-input').forEach(input => {
        input.addEventListener('change', () => {
            const box = input.parentElement.querySelector('.thumbs');
            box.innerHTML = '';
            [...input.files].forEach(f => {
                const img = document.createElement('img');
                img.src = URL.createObjectURL(f);
                img.onload = () => URL.revokeObjectURL(img.src);
                box.appendChild(img);
            });
        });
    });

    // ---- "Sab OK" shortcuts ----
    const setAllOk = root => root.querySelectorAll('.check-select').forEach(s => { s.value = 'OK'; });
    document.querySelectorAll('.set-ok').forEach(b => b.addEventListener('click', () => setAllOk(b.closest('[data-server]'))));
    document.getElementById('allOk')?.addEventListener('click', () => setAllOk(form));

    // ---- shrink photos in the browser (faster upload, smaller Excel) ----
    async function shrink(file) {
        if (!file.type.startsWith('image/') || file.type === 'image/gif') return file;
        try {
            const bmp = await createImageBitmap(file, { imageOrientation: 'from-image' });
            const scale = Math.min(1, MAX_SIDE / Math.max(bmp.width, bmp.height));
            const canvas = document.createElement('canvas');
            canvas.width = Math.round(bmp.width * scale);
            canvas.height = Math.round(bmp.height * scale);
            canvas.getContext('2d').drawImage(bmp, 0, 0, canvas.width, canvas.height);
            const blob = await new Promise(r => canvas.toBlob(r, 'image/jpeg', QUALITY));
            if (!blob || blob.size >= file.size) return file;       // keep original when it is already smaller
            return new File([blob], file.name.replace(/\.[^.]+$/, '') + '.jpg', { type: 'image/jpeg' });
        } catch { return file; }
    }

    function show(html, cls) { msg.innerHTML = `<div class="alert alert-${cls} mb-0">${html}</div>`; }

    form.addEventListener('submit', async e => {
        e.preventDefault();
        if (!form.reportValidity()) return;

        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Saving…';
        show('Photos tayyar ho rahi hain aur Google Drive par upload ho rahi hain. Page band na karein…', 'info');

        try {
            const fd = new FormData(form);
            for (const input of form.querySelectorAll('input[type=file]')) {
                fd.delete(input.name);
                for (const f of input.files) { const out = await shrink(f); fd.append(input.name, out, out.name); }
            }
            const res = await fetch(form.getAttribute('action') || location.pathname, {
                method: 'POST', body: fd, headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            let data = {};
            try { data = await res.json(); } catch { /* not json */ }
            if (!res.ok || !data.ok) throw new Error(data.error || ('Server error ' + res.status));
            location.href = data.redirect;
        } catch (err) {
            show('<b>Save nahi hui:</b> ' + esc(err.message), 'danger');
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-cloud-arrow-up"></i> Save &amp; Upload to Drive';
        }
    });
})();
