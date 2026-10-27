// Browser helpers for KanaanFlow: local storage (IndexedDB), connectivity, files, printing and install.
window.kf = (() => {
    const DB_NAME = 'kanaanflow';
    let dbPromise = null;

    function openDb() {
        if (!dbPromise) {
            dbPromise = new Promise((resolve, reject) => {
                const request = indexedDB.open(DB_NAME, 1);
                request.onupgradeneeded = () => {
                    const db = request.result;
                    db.createObjectStore('events', { keyPath: 'id' });
                    db.createObjectStore('meta');
                };
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
        }
        return dbPromise;
    }

    async function run(store, mode, work) {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const tx = db.transaction(store, mode);
            const result = work(tx.objectStore(store));
            tx.oncomplete = () => resolve(result && 'result' in result ? result.result : undefined);
            tx.onerror = () => reject(tx.error);
            tx.onabort = () => reject(tx.error);
        });
    }

    let installEvent = null;
    window.addEventListener('beforeinstallprompt', e => { e.preventDefault(); installEvent = e; });

    return {
        // ---- storage ----
        getEvents: () => run('events', 'readonly', s => s.getAll()),
        putEvents: rows => run('events', 'readwrite', s => { rows.forEach(r => s.put(r)); }),
        deleteEvents: ids => run('events', 'readwrite', s => { ids.forEach(id => s.delete(id)); }),
        clearEvents: () => run('events', 'readwrite', s => { s.clear(); }),
        markSynced: async ids => {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction('events', 'readwrite');
                const s = tx.objectStore('events');
                ids.forEach(id => {
                    const get = s.get(id);
                    get.onsuccess = () => { if (get.result) { get.result.synced = true; s.put(get.result); } };
                });
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
            });
        },
        getMeta: async key => { const v = await run('meta', 'readonly', s => s.get(key)); return v === undefined ? null : v; },
        setMeta: (key, value) => run('meta', 'readwrite', s => { s.put(value, key); }),
        persist: async () => {
            try { return navigator.storage && navigator.storage.persist ? await navigator.storage.persist() : false; }
            catch { return false; }
        },

        // ---- connectivity ----
        isOnline: () => navigator.onLine,
        watchOnline: ref => {
            window.addEventListener('online', () => ref.invokeMethodAsync('OnOnlineChanged', true));
            window.addEventListener('offline', () => ref.invokeMethodAsync('OnOnlineChanged', false));
        },

        // ---- page ----
        setDocument: (lang, dir) => {
            document.documentElement.lang = lang;
            document.documentElement.dir = dir;
        },
        reveal: id => { const el = document.getElementById(id); if (el) el.scrollIntoView({ block: 'nearest', behavior: 'smooth' }); },
        scrollTop: () => { const m = document.querySelector('.kf-main'); if (m) m.scrollTop = 0; window.scrollTo(0, 0); },
        // Inside the Android app, the native bridge prints (and saves PDFs) through Android's print service.
        print: () => setTimeout(() => window.KanaanFlowAndroid ? window.KanaanFlowAndroid.print() : window.print(), 50),

        // ---- files ----
        // Returns false when the file could not be saved.
        download: (name, mime, bytes) => {
            if (window.KanaanFlowAndroid) {
                // The Android app's WebView can't download blobs: hand the file to the app, which shares or saves it.
                let binary = '';
                for (let i = 0; i < bytes.length; i += 0x8000) binary += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
                return window.KanaanFlowAndroid.saveFile(name, mime, btoa(binary));
            }
            const blob = new Blob([bytes], { type: mime });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = name;
            document.body.appendChild(a);
            a.click();
            a.remove();
            setTimeout(() => URL.revokeObjectURL(url), 10000);
            return true;
        },
        canShareFiles: () => {
            if (window.KanaanFlowAndroid) return false;
            try { return !!(navigator.canShare && navigator.canShare({ files: [new File(['x'], 'x.txt', { type: 'text/plain' })] })); }
            catch { return false; }
        },
        share: async (name, mime, bytes) => {
            try {
                const file = new File([bytes], name, { type: mime });
                await navigator.share({ files: [file], title: name });
                return true;
            } catch { return false; }
        },

        // ---- install ----
        canInstall: () => installEvent !== null,
        isStandalone: () => !!window.KanaanFlowAndroid || window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true,
        isIos: () => /iphone|ipad|ipod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1),
        install: async () => {
            if (!installEvent) return false;
            installEvent.prompt();
            const choice = await installEvent.userChoice;
            installEvent = null;
            return choice.outcome === 'accepted';
        }
    };
})();
