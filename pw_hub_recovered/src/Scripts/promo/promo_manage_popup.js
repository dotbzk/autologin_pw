// Creates and controls the promo-management popup.
try {
    // Send a lightweight ping to verify WebMessage channel early
    try{ if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){ window.chrome.webview.postMessage('{"type":"promo_ping","ts":"'+Date.now()+'"}'); } }catch(_){}

    // Persist and restore the management window's position, size, and collapsed state.
    var PROMO_POPUP_KEY = 'promo_popup_state';
    // Bootstrap from localStorage immediately while the persistent configuration loads.
    var PROMO_POPUP_BOOTSTRAP_KEY = 'promo_popup_state_bootstrap';
    // Forward JavaScript diagnostics to .NET.
    function promoLog(eventName, payload){
        try{
            var msg = { type:'promo_log', event:eventName, data:payload||null, ts:Date.now() };
            if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage){
                window.chrome.webview.postMessage(JSON.stringify(msg));
            }
        }catch(_){ }
    }

    var __promoPopup_state = null;

    function ensurePwHubAppConfigShim(){
        try{
            if (window.pwHubAppConfig && (window.pwHubAppConfig.get || window.pwHubAppConfig.set)){
                try{ promoLog('ensurePwHubAppConfig_exists', { hasPwHub:true }); }catch(__){}
                return;
            }
            var hasChrome = !!(window.chrome && window.chrome.webview && window.chrome.webview.postMessage);
            try{ promoLog('ensurePwHubAppConfig_create_attempt', { hasChrome: hasChrome }); }catch(__){}
            if (!hasChrome) return;

            var pending = new Map();
            var nextId = 1;
            function tryLocalGet(key, def){ try{ var raw = localStorage.getItem(key); return raw!=null ? JSON.parse(raw) : def; }catch(_){ return def; } }
            function tryLocalSet(key, val){ try{ localStorage.setItem(key, JSON.stringify(val)); }catch(_){ } }

            var shim = {
                get: function(key, def){
                    return new Promise(function(resolve){
                        var id = nextId++;
                        pending.set(id, resolve);
                        try{
                            window.chrome.webview.postMessage({ type: 'appConfig:get', id: id, key: key });
                        }catch(_){
                            pending.delete(id);
                            resolve(tryLocalGet(key, def));
                            return;
                        }
                        setTimeout(function(){
                            if (pending.has(id)){
                                pending.delete(id);
                                resolve(tryLocalGet(key, def));
                            }
                        }, 800);
                    });
                },
                set: function(key, val){
                    return new Promise(function(resolve){
                        var id = nextId++;
                        pending.set(id, resolve);
                        try{
                            window.chrome.webview.postMessage({ type: 'appConfig:set', id: id, key: key, value: val });
                        }catch(_){
                            pending.delete(id);
                            tryLocalSet(key, val);
                            resolve(true);
                            return;
                        }
                        setTimeout(function(){
                            if (pending.has(id)){
                                pending.delete(id);
                                tryLocalSet(key, val);
                                resolve(true);
                            }
                        }, 800);
                    });
                }
            };

            window.pwHubAppConfig = shim;
            try{
                if (!window.appConfig){ window.appConfig = {}; }
                if (!window.appConfig.get) window.appConfig.get = shim.get;
                if (!window.appConfig.set) window.appConfig.set = shim.set;
            }catch(__){}

            if (window.chrome && window.chrome.webview){
                window.chrome.webview.addEventListener('message', function(ev){
                    try{
                        var msg = ev && ev.data;
                        if (!msg || msg.type !== 'appConfig:resp') return;
                        var res = pending.get(msg.id);
                        if (!res) return;
                        pending.delete(msg.id);
                        if ('value' in msg) res(msg.value); else res(true);
                    }catch(__){}
                });
            }

            try{ promoLog('ensurePwHubAppConfig_created', { hasPwHub: !!window.pwHubAppConfig }); }catch(__){}
        }catch(e){
            try{ promoLog('ensurePwHubAppConfig_exception', e && e.message ? e.message : String(e)); }catch(__){}
        }
    }

    ensurePwHubAppConfigShim();

    function getPromoAppConfig(){
        try{
            var hasPw = !!(window.pwHubAppConfig && (window.pwHubAppConfig.get || window.pwHubAppConfig.set));
            var hasApp = !!(window.appConfig && (window.appConfig.get || window.appConfig.set));
            try{ promoLog('getPromoAppConfig_probe', { hasPwHub: hasPw, hasAppConfig: hasApp }); }catch(__){}
            if (hasPw) return window.pwHubAppConfig;
            if (hasApp) return window.appConfig;
        }catch(_){ }
        try{ promoLog('getPromoAppConfig_null', null); }catch(__){}
        return null;
    }

    function savePopupState(el, state){
        try{
            state = state || {};
            __promoPopup_state = state;
            promoLog('savePopupState', state);
            try{
                localStorage.setItem(PROMO_POPUP_BOOTSTRAP_KEY, JSON.stringify(state));
            }catch(__){}
            try{
                var appCfg = getPromoAppConfig();
                if (appCfg && appCfg.set){
                    appCfg.set(PROMO_POPUP_KEY, state)
                        .then(function(){ try{ promoLog('savePopupState_ok', null); }catch(__){} })
                        .catch(function(err){ try{ promoLog('savePopupState_err', (err && (err.message||err)) || 'unknown'); }catch(__){} });
                } else {
                    promoLog('savePopupState_no_appConfig', null);
                }
            }catch(e){
                promoLog('savePopupState_exception', e && e.message ? e.message : String(e));
            }
        }catch(e){
            promoLog('savePopupState_outer_exception', e && e.message ? e.message : String(e));
        }
    }

    function loadPopupState(){
        try{
            if (__promoPopup_state != null){
                return __promoPopup_state;
            }

            try{
                var rawBootstrap = localStorage.getItem(PROMO_POPUP_BOOTSTRAP_KEY);
                if (rawBootstrap){
                    try{
                        var cached = JSON.parse(rawBootstrap);
                        if (cached && typeof cached === 'object'){
                            __promoPopup_state = cached;
                            if (popup){
                                try{
                                    if (cached.width) popup.style.width = cached.width + 'px';
                                    if (cached.height) popup.style.height = cached.height + 'px';
                                    if (cached.useLeftTop){
                                        popup.style.left = (cached.left||16) + 'px';
                                        popup.style.top = (cached.top||16) + 'px';
                                        popup.style.right = '';
                                        popup.style.bottom = '';
                                    } else {
                                        popup.style.right = (cached.right||16) + 'px';
                                        popup.style.bottom = (cached.bottom||16) + 'px';
                                        popup.style.left = '';
                                        popup.style.top = '';
                                    }
                                    try{
                                        if (typeof cached.collapsed === 'boolean'){
                                            setCollapsed(!!cached.collapsed);
                                        }
                                    }catch(__){}
                                }catch(__){}
                            }
                            return __promoPopup_state;
                        }
                    }catch(__){}
                }
            }catch(__){}

            try{
                var appCfg = getPromoAppConfig();
                if (!window.__promoPopup_stateLoading && appCfg && appCfg.get){
                    window.__promoPopup_stateLoading = true;
                    promoLog('loadPopupState_fetch_start', null);
                    appCfg.get(PROMO_POPUP_KEY, null).then(function(st){
                        window.__promoPopup_stateLoading = false;
                        try{
                            __promoPopup_state = st || null;
                            promoLog('loadPopupState_fetch_ok', st);
                            if (!st || !popup) return;
                            try{
                                if (st.width) popup.style.width = st.width + 'px';
                                if (st.height) popup.style.height = st.height + 'px';
                                if (st.useLeftTop){
                                    popup.style.left = (st.left||16) + 'px';
                                    popup.style.top = (st.top||16) + 'px';
                                    popup.style.right = '';
                                    popup.style.bottom = '';
                                } else {
                                    popup.style.right = (st.right||16) + 'px';
                                    popup.style.bottom = (st.bottom||16) + 'px';
                                    popup.style.left = '';
                                    popup.style.top = '';
                                }
                                try{ setCollapsed(!!st.collapsed); }catch(__){}
                            }catch(__){}
                        }catch(ex){
                            promoLog('loadPopupState_apply_exception', ex && ex.message ? ex.message : String(ex));
                        }
                    }).catch(function(err){
                        window.__promoPopup_stateLoading = false;
                        promoLog('loadPopupState_fetch_err', (err && (err.message||err)) || 'unknown');
                    });
                }
            }catch(e){
                promoLog('loadPopupState_schedule_exception', e && e.message ? e.message : String(e));
            }

            return __promoPopup_state;
        }catch(e){
            promoLog('loadPopupState_outer_exception', e && e.message ? e.message : String(e));
            return __promoPopup_state;
        }
    }

    // Create floating popup if not exists
    var popup = document.getElementById('promo_popup');
    var header, contentWrap, compact;
    var collapsed = false;

    function getToggleBtn(){
        try{ return (header ? header.querySelector('button') : null) || document.querySelector('#promo_popup > div button'); }catch(e){ return null; }
    }

    function recalcContentHeight(){
        try{
            if (!popup || !contentWrap) return;
            var h = header && header.style.display !== 'none' ? header.offsetHeight : 0;
            var total = popup.getBoundingClientRect().height;
            var contentH = Math.max(0, total - h);
            contentWrap.style.maxHeight = contentH + 'px';
            contentWrap.style.height = contentH + 'px';
        }catch(e){}
    }

    function setCollapsed(c){
        try{
            collapsed = !!c;
            var st = loadPopupState() || {};
            st.collapsed = collapsed;
            var res = document.getElementById('promo_popup_resizer');
            if (collapsed){
                // save current size before collapsing
                try{
                    var rect = popup.getBoundingClientRect();
                    st.width = rect.width; st.height = rect.height;
                }catch(ex){}
                // hide full UI
                if (contentWrap) contentWrap.style.display = 'none';
                if (header) header.style.display = 'none';
                if (res) res.style.display = 'none';
                // shrink container to compact pill
                popup.style.width = 'auto';
                popup.style.height = 'auto';
                popup.style.minWidth = '0px';
                popup.style.minHeight = '0px';
                if (compact) compact.style.display = 'inline-block';
            } else {
                if (compact) compact.style.display = 'none';
                if (header) header.style.display = 'flex';
                if (contentWrap) contentWrap.style.display = 'block';
                if (res) res.style.display = 'block';
                // restore size if known
                var s2 = loadPopupState() || {};
                if (s2.width) popup.style.width = s2.width + 'px';
                if (s2.height) popup.style.height = s2.height + 'px';
                // restore mins to defaults
                popup.style.minWidth = '240px';
                popup.style.minHeight = '120px';
                // recalc content height
                recalcContentHeight();
            }
            var toggleBtn = getToggleBtn();
            if (toggleBtn){ toggleBtn.innerText = collapsed ? '+' : '−'; }
            savePopupState(popup, st);
        }catch(e){}
    }
    if (!popup) {
        popup = document.createElement('div');
        popup.id = 'promo_popup';
        popup.style = [
            'position: fixed',
            'right: 12px', // Keep the popup close to the viewport edge.
            'bottom: 12px',
            'z-index: 2147483647',
            'background: #F6F1E7',
            'border: 1px solid #E2D8C9',
            'border-radius: 10px',
            'box-shadow: 0 8px 24px rgba(0,0,0,0.25)',
            'overflow: hidden',
            'color: #333',
            'font-family: Arial, sans-serif',
            'width: 300px',
            'min-width: 220px',
            'max-width: 340px',
            'min-height: 120px',
            'max-height: 70vh'
        ].join(';');

        header = document.createElement('div');
        header.style = 'display:flex;align-items:center;justify-content:space-between;padding:6px 10px;background:#EDE4D6;border-bottom:1px solid #E2D8C9;cursor:move;user-select:none;';
        var hTitle = document.createElement('div');
        hTitle.innerHTML = 'M<span class="lower">anagement</span>';
        hTitle.style = 'font-weight:700;color:#2c4a8d;font-size:13px;letter-spacing:0.2px;';
        var toggleBtn = document.createElement('button');
        toggleBtn.innerText = '−';
        toggleBtn.title = 'Collapse';
        toggleBtn.style = 'border:none;background:#D2C0BE;color:#333;border-radius:12px;padding:1px 6px;cursor:pointer;font-size:12px;line-height:16px;';

        contentWrap = document.createElement('div');
        contentWrap.id = 'promo_popup_content';
        contentWrap.style = 'padding:8px;overflow:auto;font-size:12px;';

        var container = document.createElement('div');
        container.id = 'promo_container';

        var resizer = document.createElement('div');
        resizer.id = 'promo_popup_resizer';
        resizer.style = 'display:none;position:absolute;width:14px;height:14px;right:2px;bottom:2px;cursor:default;background:transparent;';
        resizer.innerHTML = '<svg width="14" height="14" viewBox="0 0 14 14" style="display:block"><path d="M2 12 L12 2 L12 12 Z" fill="#00000022"/></svg>';

        // compact view (as a tiny movable button)
        compact = document.createElement('div');
        compact.id = 'promo_popup_compact';
        compact.style = 'display:none; margin:4px; padding:4px 10px; background:#EDE4D6; color:#2c4a8d; font-weight:700; font-size:12px; border-radius:14px; cursor:move; user-select:none; box-shadow: inset 0 0 0 1px #E2D8C9; width:max-content;';
        compact.title = 'Expand the management panel';
        compact.innerText = 'Management';

        contentWrap.appendChild(container);
        header.appendChild(hTitle);
        header.appendChild(toggleBtn);
        popup.appendChild(header);
        popup.appendChild(contentWrap);
        popup.appendChild(resizer);
        popup.appendChild(compact);
        document.body.appendChild(popup);

        // Toggle/compact handlers with local state
        toggleBtn.onclick = function(){ setCollapsed(!collapsed); };
        compact.addEventListener('click', function(e){
            try{
                if (compact && (compact.dataset.dragMoved === '1' || compact.dataset.dragJustDragged === '1')){
                    e.preventDefault(); e.stopPropagation();
                    return false;
                }
                setCollapsed(false);
            }catch(ex){}
        });
    } else {
        // if popup already exists, fetch sub-elements
        header = popup.firstElementChild; // expected header
        contentWrap = document.getElementById('promo_popup_content');
        compact = document.getElementById('promo_popup_compact');
        if (!compact){
            compact = document.createElement('div');
            compact.id = 'promo_popup_compact';
            compact.style = 'display:none; margin:4px; padding:4px 10px; background:#EDE4D6; color:#2c4a8d; font-weight:700; font-size:12px; border-radius:14px; cursor:move; user-select:none; box-shadow: inset 0 0 0 1px #E2D8C9; width:max-content;';
            compact.title = 'Expand the management panel';
            compact.innerText = 'Management';
            popup.appendChild(compact);
        }
        var tbtn = getToggleBtn();
        if (tbtn){ tbtn.onclick = function(){ setCollapsed(!(loadPopupState()||{}).collapsed); }; }
        if (compact){
            compact.addEventListener('click', function(e){
                try{
                    if (compact && (compact.dataset.dragMoved === '1' || compact.dataset.dragJustDragged === '1')){
                        e.preventDefault(); e.stopPropagation();
                        return false;
                    }
                    setCollapsed(false);
                }catch(ex){}
            });
        }
    }

    // initialize drag/resize once
    if (popup && !popup.getAttribute('data-draggable-inited')){
        popup.setAttribute('data-draggable-inited','1');

        function applyContentHeight(){
            try{
                if (!popup || !contentWrap) return;
                var headH = header ? header.offsetHeight : 0;
                var total = popup.clientHeight; // More accurate for the container than getBoundingClientRect.
                var cs = window.getComputedStyle(contentWrap);
                var padV = (parseFloat(cs.paddingTop)||0) + (parseFloat(cs.paddingBottom)||0);
                var contentH = Math.max(0, total - headH);
                var blockH = Math.max(0, contentH - padV);
                contentWrap.style.maxHeight = contentH + 'px';
                contentWrap.style.height = blockH + 'px';
            }catch(e){}
        }

        var __autosizeTimer = null;
        function scheduleAutosize(delay){
            try{
                if (__autosizeTimer) { clearTimeout(__autosizeTimer); __autosizeTimer = null; }
                __autosizeTimer = setTimeout(function(){
                    try{ requestAnimationFrame(function(){ requestAnimationFrame(function(){ autosizePopupIfNoState(true); }); }); }catch(e){ try{ autosizePopupIfNoState(true); }catch(_){} }
                }, typeof delay==='number'? delay : 50);
            }catch(e){}
        }

        function autosizePopupIfNoState(force){
            try{
                if (!popup || !contentWrap) return;
                var st0 = loadPopupState() || {};
                if (st0 && st0.collapsed) return;

                var prevOverflow = contentWrap.style.overflow;
                var prevH = contentWrap.style.height;
                var prevMaxH = contentWrap.style.maxHeight;
                var prevW = popup.style.width;
                var prevMinW = popup.style.minWidth;
                var prevMinH = popup.style.minHeight;
                contentWrap.style.overflow = 'visible';
                contentWrap.style.height = 'auto';
                contentWrap.style.maxHeight = 'none';

                var inner = contentWrap.firstElementChild || contentWrap;
                var cs = window.getComputedStyle(contentWrap);
                var padV = (parseFloat(cs.paddingTop)||0) + (parseFloat(cs.paddingBottom)||0);
                var padH = (parseFloat(cs.paddingLeft)||0) + (parseFloat(cs.paddingRight)||0);

                var headH = header ? header.offsetHeight : 0;
                var cScrollH = inner.scrollHeight + padV;
                try{
                    var last = inner.lastElementChild;
                    if (last){
                        var lcs = window.getComputedStyle(last);
                        var mb = parseFloat(lcs.marginBottom)||0;
                        if (mb > 0) cScrollH += mb;
                    }
                }catch(__){}
                var desiredH = headH + cScrollH + 4;
                var maxH = Math.floor(window.innerHeight * 0.8); // Keep this aligned with CSS max-height: 80vh.
                var minH = 140; // Keep this aligned with the stylesheet.
                var newH = Math.max(minH, Math.min(maxH, desiredH));

                var headW = header ? header.scrollWidth : 0;
                var cScrollW = inner.scrollWidth + padH;
                var desiredW = Math.max(260, Math.max(headW, cScrollW));
                var maxW = Math.floor(window.innerWidth * 0.5); // Keep this aligned with CSS max-width: 50vw.
                var newW = Math.max(260, Math.min(maxW, desiredW));

                popup.style.width = newW + 'px';
                popup.style.height = newH + 'px';

                contentWrap.style.overflow = prevOverflow;
                contentWrap.style.height = prevH;
                contentWrap.style.maxHeight = prevMaxH;
                requestAnimationFrame(function(){ applyContentHeight(); });

                try{
                    var stSave = loadPopupState() || {};
                    stSave.width = newW;
                    stSave.height = newH;
                    savePopupState(popup, stSave);
                }catch(__){}

                popup.setAttribute('data-autosized','1');
            }catch(e){}
        }

        // restore state
        (function(){
            var st = loadPopupState();
            if (!st) {
                try{ autosizePopupIfNoState(); }catch(e){}
                try{ applyContentHeight(); }catch(e){}
                return;
            }
            // size
            if (st.width) popup.style.width = st.width + 'px';
            if (st.height) popup.style.height = st.height + 'px';
            // position
            if (st.useLeftTop){
                popup.style.left = (st.left||16) + 'px';
                popup.style.top = (st.top||16) + 'px';
                popup.style.right = '';
                popup.style.bottom = '';
            } else {
                popup.style.right = (st.right||16) + 'px';
                popup.style.bottom = (st.bottom||16) + 'px';
                popup.style.left = '';
                popup.style.top = '';
            }
            // apply collapsed or expanded view via helper if available
            try{ setCollapsed(!!st.collapsed); }catch(e){}
            if (!st.width && !st.height && !st.collapsed) { try{ autosizePopupIfNoState(); }catch(e){} }
            try{ applyContentHeight(); }catch(e){}
        })();

        // drag logic (by header or compact pill)
        (function(){
            if (!header && !compact) return;
            var dragging = false, startX=0, startY=0, startLeft=0, startTop=0;
            var moved = false; var moveThreshold = 4; // px
            function onMouseDown(e){
                // allow clicking controls in header (like toggle btn) without drag
                if (e.target && (e.target.tagName==='BUTTON' || e.target.closest('button'))) return;
                dragging = true;
                moved = false;
                if (header){ delete header.dataset.dragMoved; delete header.dataset.dragJustDragged; }
                if (compact){ delete compact.dataset.dragMoved; delete compact.dataset.dragJustDragged; }
                var rect = popup.getBoundingClientRect();
                // switch to left/top coordinates once dragging starts
                var st = loadPopupState() || {};
                if (!st.useLeftTop){
                    // compute left/top from current right/bottom
                    var left = window.innerWidth - rect.right;
                    var top = rect.top;
                    popup.style.left = (rect.left) + 'px';
                    popup.style.top = (rect.top) + 'px';
                    popup.style.right = '';
                    popup.style.bottom = '';
                    st.useLeftTop = true;
                    st.left = rect.left;
                    st.top = rect.top;
                    savePopupState(popup, st);
                }
                startX = e.clientX; startY = e.clientY;
                startLeft = parseFloat(popup.style.left || rect.left + '');
                startTop = parseFloat(popup.style.top || rect.top + '');
                document.addEventListener('mousemove', onMouseMove);
                document.addEventListener('mouseup', onMouseUp);
                e.preventDefault();
            }
            function onMouseMove(e){
                if (!dragging) return;
                var dx = e.clientX - startX;
                var dy = e.clientY - startY;
                if (!moved && (Math.abs(dx) > moveThreshold || Math.abs(dy) > moveThreshold)){
                    moved = true;
                    if (header){ header.dataset.dragMoved = '1'; }
                    if (compact){ compact.dataset.dragMoved = '1'; }
                }
                var newLeft = startLeft + dx;
                var newTop = startTop + dy;
                // constrain
                var r = popup.getBoundingClientRect();
                var maxLeft = window.innerWidth - r.width;
                var maxTop = window.innerHeight - r.height;
                newLeft = Math.min(Math.max(0, newLeft), Math.max(0, maxLeft));
                newTop = Math.min(Math.max(0, newTop), Math.max(0, maxTop));
                popup.style.left = newLeft + 'px';
                popup.style.top = newTop + 'px';
            }
            function onMouseUp(){
                if (!dragging) return;
                dragging = false;
                document.removeEventListener('mousemove', onMouseMove);
                document.removeEventListener('mouseup', onMouseUp);
                var st = loadPopupState() || { useLeftTop: true };
                var rect = popup.getBoundingClientRect();
                st.left = rect.left; st.top = rect.top;
                savePopupState(popup, st);
                if (moved){
                    if (header){
                        header.dataset.dragJustDragged = '1';
                        setTimeout(function(){ delete header.dataset.dragJustDragged; delete header.dataset.dragMoved; }, 120);
                    }
                    if (compact){
                        compact.dataset.dragJustDragged = '1';
                        setTimeout(function(){ delete compact.dataset.dragJustDragged; delete compact.dataset.dragMoved; }, 120);
                    }
                }
            }
            if (header) header.addEventListener('mousedown', onMouseDown);
            if (compact) compact.addEventListener('mousedown', onMouseDown);
            if (header){
                header.addEventListener('click', function(e){
                    try{
                        if (e.target && (e.target.tagName==='BUTTON' || e.target.closest('button'))) return;
                        if (header.dataset.dragMoved === '1' || header.dataset.dragJustDragged === '1'){
                            e.preventDefault(); e.stopPropagation();
                            return false;
                        }
                        var st = loadPopupState() || {};
                        var isCollapsed = !!st.collapsed;
                        setCollapsed(!isCollapsed);
                    }catch(ex){}
                });
            }
        })();

        // resize logic (by handle)
        (function(){
            var handle = document.getElementById('promo_popup_resizer');
            if (!handle) return;
            function onDown(e){ try{ e.preventDefault(); }catch(_){ } }
            function onMove(e){ /* no-op */ }
            function onUp(){ /* no-op */ }
            handle.addEventListener('mousedown', onDown);
            // ensure initial content sizing
            scheduleAutosize(0);
            requestAnimationFrame(function(){ applyContentHeight(); });
            window.addEventListener('resize', function(){
                applyContentHeight();
                scheduleAutosize(50);
            });
        })();
    }

    // Build controls only once inside promo_container
    if (!document.getElementById('selectAllBtn')) {
        var pillBtnCss = [
            'margin:0',
            'font-size:12px',
            'line-height:18px',
            'font-weight:500',
            'border:none',
            'cursor:pointer',
            'padding:2px 10px',
            'border-radius:999px',
            '-webkit-appearance:button',
            'text-rendering:auto',
            'display:inline-block',
            'text-align:center',
            'white-space:nowrap',
            'background-color:#D2C0BE',
            'color:#2c2c2c'
        ].join(';');
        var rowCss = 'display:flex; align-items:center; gap:6px; flex-wrap:wrap; margin-bottom:3px;';

        var buttonContainer = document.createElement('div');
        buttonContainer.style = 'display:flex; flex-direction:column; gap:6px; margin-top:2px;';

        var panelTitle = document.createElement('div');
        panelTitle.textContent = 'Quick Item Selection';
        panelTitle.style = 'font-size:11px; text-transform:uppercase; letter-spacing:0.6px; color:#7a6a54; margin:0 0 2px 2px;';
        buttonContainer.append(panelTitle);

        // Helpers: selection by label
        var selectAll = document.createElement('button');
        selectAll.innerText = 'Select All';
        selectAll.id = 'selectAllBtn';
        selectAll.style = pillBtnCss;
        selectAll.onclick = function() {
            var checkboxes = document.querySelectorAll('.items_container input[type=checkbox]');
            checkboxes.forEach(function(checkbox) {
                if (!checkbox.checked){ checkbox.checked = true; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} }
            });
            try{ if (window.__promoSelected_schedule) window.__promoSelected_schedule(0); }catch(e){}
            return false;
        };

        var clearAll = document.createElement('button');
        clearAll.innerText = 'Clear All';
        clearAll.id = 'clearAllBtn';
        clearAll.style = pillBtnCss;
        clearAll.onclick = function() {
            var checkboxes = document.querySelectorAll('.items_container input[type=checkbox]');
            checkboxes.forEach(function(checkbox) {
                if (checkbox.checked){ checkbox.checked = false; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} }
            });
            try{ if (window.__promoSelected_schedule) window.__promoSelected_schedule(0); }catch(e){}
            return false;
        };

        var selectByLabelTextRegex = function(pattern) {
            var labels = document.querySelectorAll('.items_container label');
            labels.forEach(function(label) {
                if (pattern.test((label.innerText||'').toLowerCase())) {
                    var checkboxId = label.getAttribute('for');
                    var checkbox = document.getElementById(checkboxId);
                    if (checkbox && checkbox.type === 'checkbox') {
                        var should = true;
                        if (!checkbox.checked){ checkbox.checked = true; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} }
                    }
                }
            });
        };

        var selectByLabelTextRegexes = function(patterns) {
            patterns.forEach(function(pattern) { selectByLabelTextRegex(pattern); });
        };

        var selectByLabelText = function(text) {
            var labels = document.querySelectorAll('.items_container label');
            labels.forEach(function(label) {
                if ((label.innerText||'').toLowerCase().includes(text.toLowerCase())) {
                    var checkboxId = label.getAttribute('for');
                    var checkbox = document.getElementById(checkboxId);
                    if (checkbox && checkbox.type === 'checkbox') {
                        if (!checkbox.checked){ checkbox.checked = true; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} }
                    }
                }
            });
        };

        var selectByLabelTexts = function(texts) { texts.forEach(function(text){ selectByLabelText(text); }); };

        // Unselect helpers
        var unselectByLabelPattern = function(pattern){
            var labels = document.querySelectorAll('.items_container label');
            labels.forEach(function(label){
                var text = (label.innerText||'').toLowerCase();
                if (pattern.test(text)){
                    var checkbox = document.getElementById(label.getAttribute('for'));
                    if (checkbox && checkbox.type==='checkbox') { if (checkbox.checked){ checkbox.checked = false; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} } }
                }
            });
        };
        var unselectByLabelPatterns = function(patterns){ patterns.forEach(function(p){ unselectByLabelPattern(p); }); };
        var unselectByLabelText = function(text) {
            var labels = document.querySelectorAll('.items_container label');
            labels.forEach(function(label) {
                if ((label.innerText||'').toLowerCase().includes(text.toLowerCase())) {
                    var checkboxId = label.getAttribute('for');
                    var checkbox = document.getElementById(checkboxId);
                    if (checkbox && checkbox.type === 'checkbox') { if (checkbox.checked){ checkbox.checked = false; try{ checkbox.dispatchEvent(new Event('change', { bubbles: true })); }catch(e){} } }
                }
            });
        };
        var unselectByLabelTexts = function(texts) { texts.forEach(function(text){ unselectByLabelText(text); }); };

        // ================= Items/Chests Grid View (Main page) =================
        (function(){
            try{
                // State: list | grid
                var VIEW_KEY = 'promo_items_view_mode';
                var __promoViewMode_cache = null;

                function saveViewMode(mode){
                    try{
                        if (mode !== 'grid' && mode !== 'list') return;
                        __promoViewMode_cache = mode;
                        try{ localStorage.setItem(VIEW_KEY, mode); }catch(_){ }
                        if (window.appConfig && window.appConfig.set){
                            window.appConfig.set(VIEW_KEY, mode);
                        }
                    }catch(e){}
                }

                function loadViewMode(defaultMode){
                    try{
                        defaultMode = defaultMode || 'list';
                        if (__promoViewMode_cache === 'grid' || __promoViewMode_cache === 'list') return __promoViewMode_cache;
                        try{
                            var v = localStorage.getItem(VIEW_KEY);
                            if (v === 'grid' || v === 'list'){
                                __promoViewMode_cache = v;
                                return v;
                            }
                        }catch(_){ }
                        return defaultMode;
                    }catch(_){ return defaultMode || 'list'; }
                }

                function loadViewModeAsync(defaultMode){
                    try{
                        defaultMode = loadViewMode(defaultMode || 'list');
                        if (window.appConfig && window.appConfig.get){
                            return window.appConfig.get(VIEW_KEY, defaultMode).then(function(v){
                                try{
                                    if (v === 'grid' || v === 'list'){
                                        __promoViewMode_cache = v;
                                        try{ localStorage.setItem(VIEW_KEY, v); }catch(__){}
                                        return v;
                                    }
                                    return defaultMode;
                                }catch(__){ return defaultMode; }
                            }).catch(function(){ return defaultMode; });
                        }
                        return Promise.resolve(defaultMode);
                    }catch(_){ return Promise.resolve(loadViewMode(defaultMode)); }
                }

                // Create composite host once
                var tableCont = document.querySelector('.items_container');
                var composite = document.getElementById('promo_items_composite');
                if (!composite && tableCont && tableCont.parentElement){
                    composite = document.createElement('div');
                    composite.id = 'promo_items_composite';
                    composite.style = 'display:none; padding:8px 6px; display:flex; flex-direction:column; gap:12px;';

                    var secItems = document.createElement('div');
                    secItems.id = 'promo_items_section_items';
                    secItems.style = 'display:flex; flex-direction:column; gap:8px;';
                    var secItemsTitle = document.createElement('div');
                    secItemsTitle.textContent = 'Items';
                    secItemsTitle.title = 'Show all selectable items';
                    secItemsTitle.style = 'color:#2c4a8d; font-weight:700;';
                    var gridHost = document.createElement('div');
                    gridHost.id = 'promo_items_grid';
                    gridHost.style = 'padding:4px 0;';
                    secItems.appendChild(secItemsTitle);
                    secItems.appendChild(gridHost);

                    var secChests = document.createElement('div');
                    secChests.id = 'promo_items_section_chests';
                    secChests.style = 'display:flex; flex-direction:column; gap:8px;';
                    var secChestsTitle = document.createElement('div');
                    secChestsTitle.textContent = 'Chests';
                    secChestsTitle.title = 'Click a chest to open its activation link';
                    secChestsTitle.style = 'color:#2c4a8d; font-weight:700;';
                    var chestsHost = document.createElement('div');
                    chestsHost.id = 'promo_chests_grid';
                    chestsHost.style = 'padding:4px 0;';
                    secChests.appendChild(secChestsTitle);
                    secChests.appendChild(chestsHost);

                    composite.appendChild(secItems);
                    composite.appendChild(secChests);

                    // place right after the table container
                    tableCont.parentElement.insertBefore(composite, tableCont.nextSibling);
                }

                // Tooltip (single)
                var tip = document.getElementById('promo_items_grid_tooltip');
                if (!tip){
                    tip = document.createElement('div');
                    tip.id = 'promo_items_grid_tooltip';
                    tip.style = [
                        'position: fixed',
                        'z-index: 2147483647',
                        'pointer-events: none',
                        'display: none',
                        'max-width: 360px',
                        'background: #ffffff',
                        'color: #222',
                        'border: 1px solid #E2D8C9',
                        'box-shadow: 0 8px 24px rgba(0,0,0,0.25)',
                        'border-radius: 8px',
                        'padding: 8px 10px',
                        'font-family: Arial, sans-serif',
                        'font-size: 13px',
                        'line-height: 1.35'
                    ].join(';');
                    document.body.appendChild(tip);
                }

                // Build maps from table
                var __MS_PER_DAY = 24 * 60 * 60 * 1000;
                var __itemsMap = {};
                var __promoItemsSortMode = 'expiry';
                var __promoItemsGroupByType = true;

                function extractExpiryText(text){
                    try{
                        if (!text) return null;
                        text = (text || '').toString();
                        text = text.replace(/\s+/g, ' ').trim();
                        var m = text.match(/(\d{1,2}\.\d{1,2}\.\d{2,4}(?:\s+\d{1,2}:\d{2})?)/);
                        if (!m) return null;
                        return m[1];
                    } catch(_) { return null; }
                }

                function parseExpiryDate(text){
                    try{
                        var main = extractExpiryText(text);
                        if (!main) return null;
                        var parts = main.split(' ');
                        var d = parts[0].split('.');
                        var day = parseInt(d[0], 10) || 0;
                        var month = parseInt(d[1], 10) || 0;
                        var year = parseInt(d[2], 10) || 0;
                        if (year < 100) year += 2000;
                        var hh = 0, mm = 0;
                        if (parts.length > 1){
                            var t = parts[1].split(':');
                            hh = parseInt(t[0], 10) || 0;
                            mm = parseInt(t[1], 10) || 0;
                        }
                        if (!day || !month || !year) return null;
                        var dt = new Date(year, month - 1, day, hh, mm, 0, 0);
                        if (isNaN(dt.getTime())) return null;
                        return dt.getTime();
                    } catch(_) { return null; }
                }

                function rebuildItemsMap(){
                    try{
                        __itemsMap = {};
                        var rows = document.querySelectorAll('.items_container tr');
                        rows.forEach(function(tr){
                            try{
                                var label = tr.querySelector('.item_input_block label');
                                var input = tr.querySelector('.item_input_block input[type=checkbox]');
                                if (!label || !input) return;
                                var nameRaw = (label.innerText||'').trim();
                                nameRaw = nameRaw.replace(/\(до [^)]+\)/g, '').replace(/\s+/g,' ').trim();
                                var img = tr.querySelector('.img_item_cell img');
                                var src = img ? img.getAttribute('src') : '';
                                if (src && src.startsWith('//')) src = window.location.protocol + src;
                                var descSpan = tr.querySelector('.img_item_cont span');
                                var desc = '';
                                var descPlain = '';
                                if (descSpan){
                                    try{
                                        desc = (descSpan.innerHTML || descSpan.innerText || '').toString();
                                    }catch(_){ desc=''; }
                                    try{
                                        descPlain = (descSpan.innerText || descSpan.textContent || '').toString();
                                    }catch(_){ descPlain = ''; }
                                }

                                var itemType = '';
                                try{
                                    var srcText = descPlain || desc;
                                    if (srcText){
                                        var mLine = srcText.match(/Тип\s*:[^\n\r]*/i);
                                        var line = mLine ? mLine[0] : '';
                                        if (line){
                                            var mType = line.match(/Тип\s*:\s*(.+)$/i);
                                            if (mType && mType[1]){
                                                itemType = (mType[1]||'').replace(/\s+/g,' ').trim();
                                            }
                                        }
                                    }
                                }catch(__){ itemType = ''; }

                                var expiryMs = null;
                                var expiryText = null;
                                try{
                                    var dateNode = tr.querySelector('.item_input_block .date_end') || tr.querySelector('.date_end');
                                    if (dateNode){
                                        var dtText = (dateNode.textContent || dateNode.innerText || '').trim();
                                        expiryMs = parseExpiryDate(dtText);
                                        expiryText = extractExpiryText(dtText);
                                    }
                                }catch(_){ expiryMs = null; expiryText = null; }

                                var idx = Object.keys(__itemsMap).length;
                                __itemsMap[input.id] = {
                                    id: input.id,
                                    cb: input,
                                    name: nameRaw,
                                    img: src,
                                    desc: desc,
                                    expiryMs: expiryMs,
                                    expiryText: expiryText,
                                    type: itemType,
                                    index: idx
                                };
                            }catch(ex){}
                        });
                    }catch(e){}
                }

                var __chestsMap = {};
                function rebuildChestsMap(){
                    try{
                        __chestsMap = {};
                        var rows = document.querySelectorAll('.items_container tr');
                        rows.forEach(function(tr, idx){
                            try{
                                var label = tr.querySelector('.chest_input_block label');
                                var link = tr.querySelector('.chest_input_block a.chest_activate_red');
                                if (!label || !link) return;
                                var nameRaw = (label.innerText||'').trim();
                                nameRaw = nameRaw.replace(/\(до [^)]+\)/g, '').replace(/\s+/g,' ').trim();
                                var img = tr.querySelector('.img_item_cell img');
                                var src = img ? img.getAttribute('src') : '';
                                if (src && src.startsWith('//')) src = window.location.protocol + src;
                                var descSpan = tr.querySelector('.img_item_cont span');
                                var desc = '';
                                if (descSpan){
                                    try{ desc = (descSpan.innerHTML || descSpan.innerText || '').toString(); }catch(_){ desc=''; }
                                }
                                var href = link.getAttribute('href') || '';
                                // make absolute relative URLs
                                try{ if (href && href.startsWith('/')) href = location.origin + href; }catch(_){ }
                                var id = label.getAttribute('for') || ('chest_'+idx);

                                var expiryMsChest = null;
                                var expiryTextChest = null;
                                try{
                                    var dateNodeChest = tr.querySelector('.chest_input_block .date_end') || tr.querySelector('.date_end');
                                    if (dateNodeChest){
                                        var dtTextChest = (dateNodeChest.textContent || dateNodeChest.innerText || '').trim();
                                        expiryMsChest = parseExpiryDate(dtTextChest);
                                        expiryTextChest = extractExpiryText(dtTextChest);
                                    }
                                }catch(_){ expiryMsChest = null; expiryTextChest = null; }

                                __chestsMap[id] = { id: id, name: nameRaw, img: src, desc: desc, href: href, expiryMs: expiryMsChest, expiryText: expiryTextChest };
                            }catch(ex){}
                        });
                    }catch(e){}
                }

                // Render grid
                var renderTimer = null;
                function scheduleGridRender(delay){ try{ if (renderTimer){ clearTimeout(renderTimer); renderTimer=null; } renderTimer = setTimeout(function(){ renderItems(); renderChests(); }, typeof delay==='number'? delay: 30); }catch(e){} }

                function renderItems(){
                    try{
                        var gridHost = document.getElementById('promo_items_grid');
                        if (!gridHost) return;
                        gridHost.innerHTML = '';

                        var all = [];
                        Object.keys(__itemsMap).forEach(function(id){ var m=__itemsMap[id]; if (m && m.cb) all.push(m); });
                        if (!all.length) return;

                        var itemSize = 56; var cropSize = 30; var cropX = 45, cropY = 25;
                        var nowTs = Date.now();

                        function makeBlock(m){
                            var block = document.createElement('div');
                            block.className = 'promo_grid_item';
                            block.style = [
                                'width:'+itemSize+'px','height:'+itemSize+'px','border-radius:10px','box-shadow: inset 0 0 0 1px #E2D8C9','background:#ffffffCC','display:flex','align-items:center','justify-content:center','cursor:pointer','position:relative',
                                (m.cb && m.cb.checked ? 'outline:2px solid #2c4a8d; outline-offset:-2px;' : 'outline:2px solid transparent; outline-offset:-2px;')
                            ].join(';');

                            try {
                                var bg = '#ffffffCC';
                                if (typeof m.expiryMs === 'number'){
                                    var diffDays = (m.expiryMs - nowTs) / __MS_PER_DAY;
                                    if (diffDays < 0) diffDays = 0;
                                    if (diffDays < 2){
                                        bg = '#ffcccc';
                                    } else if (diffDays < 5){
                                        bg = '#fff6cc';
                                    }
                                }
                                block.style.background = bg;
                            } catch(_){ }

                            var crop = document.createElement('div');
                            crop.style = [ 'width:'+cropSize+'px','height:'+cropSize+'px','overflow:hidden','border-radius:6px','box-shadow: inset 0 0 0 1px #E2D8C9','background:#fff','position:relative' ].join(';');
                            var img = document.createElement('img'); img.src = m.img || ''; img.alt = m.name; img.title = '';
                            img.style = [ 'position:absolute','left:-'+cropX+'px','top:-'+cropY+'px','width:auto','height:auto','max-width:none','max-height:none','image-rendering:auto' ].join(';');
                            crop.appendChild(img); block.appendChild(crop);

                            // tooltip handlers
                            block.addEventListener('mouseenter', function(e){
                                try{
                                    var html = '<div style="font-weight:700; margin-bottom:4px;">'+(m.name||'')+'</div>' + ((m.desc||'').toString());
                                    if (m.expiryText){
                                        html += '<div style="margin-top:4px; color:#8b0000;">Expires: '+m.expiryText+'</div>';
                                    }
                                    tip.innerHTML = html;
                                    tip.style.display='block';
                                }catch(_){ }
                            });
                            block.addEventListener('mousemove', function(e){ try{ var x=e.clientX+14, y=e.clientY+14; var w=tip.offsetWidth||320, h=tip.offsetHeight||60; if (x+w>window.innerWidth-8) x = e.clientX - w - 10; if (y+h>window.innerHeight-8) y = e.clientY - h - 10; tip.style.left=x+'px'; tip.style.top=y+'px'; }catch(_){} });
                            block.addEventListener('mouseleave', function(){ try{ tip.style.display='none'; }catch(_){} });

                            // toggle on click
                            block.addEventListener('click', function(){ try{ if (m && m.cb){ m.cb.checked = !m.cb.checked; try{ m.cb.dispatchEvent(new Event('change', { bubbles: true })); }catch(__){} // update highlight immediately
                                if (m.cb.checked){ block.style.outline = '2px solid #2c4a8d'; block.style.outlineOffset='-2px'; } else { block.style.outline='2px solid transparent'; block.style.outlineOffset='-2px'; } } }catch(ex){} });

                            return block;
                        }

                        var groups = {};
                        var order = [];
                        if (__promoItemsGroupByType){
                            all.forEach(function(m){
                                var key = (m.type || '').toString();
                                if (!key) key = 'Other';
                                if (!groups[key]){ groups[key] = []; order.push(key); }
                                groups[key].push(m);
                            });
                        } else {
                            var keyAll = 'All Items';
                            groups[keyAll] = all.slice(0);
                            order.push(keyAll);
                        }

                        function compareItems(a,b){
                            try{
                                if (__promoItemsSortMode === 'name'){
                                    var an = (a.name||'').toLowerCase();
                                    var bn = (b.name||'').toLowerCase();
                                    if (an < bn) return -1;
                                    if (an > bn) return 1;
                                    return (a.index||0) - (b.index||0);
                                }
                                if (__promoItemsSortMode === 'expiry'){
                                    function bucket(m){
                                        if (typeof m.expiryMs !== 'number' || !m.expiryMs) return 3; // Items without a date are sorted last.
                                        var diffDays = (m.expiryMs - nowTs) / __MS_PER_DAY;
                                        if (diffDays < 0) diffDays = 0;
                                        if (diffDays < 2) return 0; // Red.
                                        if (diffDays < 5) return 1; // Yellow.
                                        return 2; // White for all remaining items.
                                    }

                                    var ba = bucket(a);
                                    var bb = bucket(b);
                                    if (ba !== bb) return ba - bb;

                                    var aen = (a.name||'').toLowerCase();
                                    var ben = (b.name||'').toLowerCase();
                                    if (aen < ben) return -1;
                                    if (aen > ben) return 1;

                                    return (a.index||0) - (b.index||0);
                                }
                            }catch(_){ }
                            return (a.index||0) - (b.index||0);
                        }

                        var root = document.createElement('div');
                        root.style = 'display:flex; flex-direction:column; gap:6px;';
                        gridHost.appendChild(root);

                        try{
                            var sortBar = document.createElement('div');
                            sortBar.style = 'display:flex; flex-wrap:wrap; align-items:center; gap:8px; font-size:12px; color:#444;';

                            var sortLabel = document.createElement('span');
                            sortLabel.textContent = 'Sort:';
                            sortBar.appendChild(sortLabel);

                            function mkSortBtn(text, mode){
                                var b = document.createElement('button');
                                b.type = 'button';
                                b.textContent = text;
                                if (mode === __promoItemsSortMode){
                                    b.style = 'padding:2px 6px; border-radius:4px; border:1px solid #2c4a8d; background:#e0ecff; cursor:pointer; font-size:12px; font-weight:600;';
                                } else {
                                    b.style = 'padding:2px 6px; border-radius:4px; border:1px solid #C6B9A4; background:#f9f5ee; cursor:pointer; font-size:12px;';
                                }
                                b.addEventListener('click', function(){ try{ __promoItemsSortMode = mode; renderItems(); }catch(_){ } });
                                return b;
                            }

                            sortBar.appendChild(mkSortBtn('site order', 'default'));
                            sortBar.appendChild(mkSortBtn('name', 'name'));
                            sortBar.appendChild(mkSortBtn('expiration date', 'expiry'));

                            var sep = document.createElement('span');
                            sep.textContent = '·';
                            sep.style = 'opacity:0.6;';
                            sortBar.appendChild(sep);

                            var groupLabel = document.createElement('label');
                            groupLabel.style = 'display:flex; align-items:center; gap:4px; cursor:pointer;';
                            var groupChk = document.createElement('input');
                            groupChk.type = 'checkbox';
                            groupChk.checked = !!__promoItemsGroupByType;
                            groupChk.addEventListener('change', function(){
                                try{
                                    __promoItemsGroupByType = !!groupChk.checked;
                                    renderItems();
                                }catch(_){ }
                            });
                            var groupText = document.createElement('span');
                            groupText.textContent = 'Group by type';
                            groupLabel.appendChild(groupChk);
                            groupLabel.appendChild(groupText);
                            sortBar.appendChild(groupLabel);

                            root.appendChild(sortBar);
                        }catch(_){ }

                        var groupsHost = document.createElement('div');
                        groupsHost.style = 'display:flex; flex-direction:column; gap:4px;';
                        root.appendChild(groupsHost);

                        order.forEach(function(key){
                            var items = groups[key] || [];
                            if (!items.length) return;

                            var headerRow = document.createElement('div');
                            headerRow.style = 'width:100%; display:flex; align-items:center; justify-content:space-between; margin:4px 0 2px 2px; gap:4px;';

                            var header = document.createElement('div');
                            header.textContent = key;
                            header.style = 'font-weight:700; font-size:13px; color:#2c4a8d;';
                            headerRow.appendChild(header);

                            var groupActions = document.createElement('div');
                            groupActions.style = 'display:flex; gap:4px; margin-right:12px;';

                            function mkGroupBtn(text, action){
                                var b = document.createElement('button');
                                b.type = 'button';
                                b.textContent = text;
                                b.style = 'padding:1px 5px; border-radius:4px; border:1px solid #C6B9A4; background:#fdf9f1; cursor:pointer; font-size:11px;';
                                b.addEventListener('click', function(){
                                    try{
                                        items.forEach(function(m){
                                            if (!m || !m.cb) return;
                                            var desired = action === 'select';
                                            if (m.cb.checked !== desired){
                                                m.cb.checked = desired;
                                                try{ m.cb.dispatchEvent(new Event('change', { bubbles:true })); }catch(_){ }
                                            }
                                        });
                                    }catch(_){ }
                                });
                                return b;
                            }

                            groupActions.appendChild(mkGroupBtn('Select All', 'select'));
                            groupActions.appendChild(mkGroupBtn('Clear All', 'unselect'));
                            headerRow.appendChild(groupActions);

                            groupsHost.appendChild(headerRow);

                            var wrap = document.createElement('div');
                            wrap.style = 'display:flex; flex-wrap:wrap; gap:8px; align-content:flex-start;';
                            groupsHost.appendChild(wrap);

                            try{ items.sort(compareItems); }catch(_){ }
                            items.forEach(function(m){
                                var blk = makeBlock(m);
                                wrap.appendChild(blk);
                            });
                        });
                    }catch(e){}
                }

                function renderChests(){
                    try{
                        var chestsHost = document.getElementById('promo_chests_grid');
                        if (!chestsHost) return;
                        chestsHost.innerHTML = '';
                        var items = []; Object.keys(__chestsMap).forEach(function(id){ var m=__chestsMap[id]; if (m) items.push(m); });
                        if (items.length === 0){
                            var sec = document.getElementById('promo_items_section_chests');
                            if (sec) sec.style.display = 'none';
                            return;
                        } else {
                            var sec = document.getElementById('promo_items_section_chests');
                            if (sec) sec.style.display = 'flex';
                        }
                        items.sort(function(a,b){ return a.name.localeCompare(b.name, 'ru'); });
                        var wrap = document.createElement('div');
                        wrap.style = 'display:flex; flex-wrap:wrap; gap:8px; align-content:flex-start;';
                        chestsHost.appendChild(wrap);

                        var itemSize = 56; var cropSize = 30; var cropX = 45, cropY = 25;
                        var nowTsChests = Date.now();
                        items.forEach(function(m){
                            var block = document.createElement('a');
                            block.className = 'promo_chest_item';
                            block.href = m.href || '#';
                            block.style = [
                                'width:'+itemSize+'px','height:'+itemSize+'px','border-radius:10px','box-shadow: inset 0 0 0 1px #E2D8C9','background:#ffffffCC','display:flex','align-items:center','justify-content:center','cursor:pointer','position:relative','text-decoration:none'
                            ].join(';');

                            try {
                                var bgChest = '#ffffffCC';
                                if (typeof m.expiryMs === 'number'){
                                    var diffDaysChest = (m.expiryMs - nowTsChests) / __MS_PER_DAY;
                                    if (diffDaysChest < 0) diffDaysChest = 0;
                                    if (diffDaysChest < 2){
                                        bgChest = '#ffcccc';
                                    } else if (diffDaysChest < 5){
                                        bgChest = '#fff6cc';
                                    }
                                }
                                block.style.background = bgChest;
                            } catch(_) { }

                            var crop = document.createElement('div');
                            crop.style = [ 'width:'+cropSize+'px','height:'+cropSize+'px','overflow:hidden','border-radius:6px','box-shadow: inset 0 0 0 1px #E2D8C9','background:#fff','position:relative' ].join(';');
                            var img = document.createElement('img'); img.src = m.img || ''; img.alt = m.name; img.title = '';
                            img.style = [ 'position:absolute','left:-'+cropX+'px','top:-'+cropY+'px','width:auto','height:auto','max-width:none','max-height:none','image-rendering:auto' ].join(';');
                            crop.appendChild(img); block.appendChild(crop);

                            // tooltip handlers (on anchor)
                            block.addEventListener('mouseenter', function(e){
                                try{
                                    var html = '<div style="font-weight:700; margin-bottom:4px;">'+(m.name||'')+'</div>' + ((m.desc||'').toString());
                                    if (m.expiryText){
                                        html += '<div style="margin-top:4px; color:#8b0000;">Expires: '+m.expiryText+'</div>';
                                    }
                                    tip.innerHTML = html;
                                    tip.style.display='block';
                                }catch(_){ }
                            });
                            block.addEventListener('mousemove', function(e){ try{ var x=e.clientX+14, y=e.clientY+14; var w=tip.offsetWidth||320, h=tip.offsetHeight||60; if (x+w>window.innerWidth-8) x = e.clientX - w - 10; if (y+h>window.innerHeight-8) y = e.clientY - h - 10; tip.style.left=x+'px'; tip.style.top=y+'px'; }catch(_){} });
                            block.addEventListener('mouseleave', function(){ try{ tip.style.display='none'; }catch(_){} });

                            // open link on click (default anchor behavior). Ensure same tab
                            block.addEventListener('click', function(e){ try{ if (block.href){ e.preventDefault(); window.location.href = block.href; } }catch(_){} });
                            wrap.appendChild(block);
                        });
                    }catch(e){}
                }

                // React to checkbox changes
                if (tableCont && !tableCont.getAttribute('data-grid-listener')){
                    tableCont.setAttribute('data-grid-listener','1');
                    tableCont.addEventListener('change', function(e){ try{ scheduleGridRender(0); }catch(_){} });
                }

                // Expose scheduler for bulk actions
                window.__promoGrid_schedule = scheduleGridRender;

                // Apply mode show/hide
                function applyViewMode(mode){
                    try{
                        mode = mode || loadViewMode();
                        promoLog('applyViewMode', { mode });

                        var gridHost = document.getElementById('promo_items_grid');
                        var chestsHost = document.getElementById('promo_chests_grid');
                        var composite = document.getElementById('promo_items_composite');
                        tableCont = document.querySelector('.items_container');
                        if (!tableCont || !composite) return;
                        if (mode === 'grid'){
                            // build map and render once visible
                            rebuildItemsMap();
                            rebuildChestsMap();
                            scheduleGridRender(0);
                            composite.style.display = 'block';
                            tableCont.style.display = 'none';
                        } else {
                            if (composite) composite.style.display = 'none';
                            tableCont.style.display = '';
                        }
                    }catch(e){}
                }
                window.__promoGrid_applyMode = applyViewMode;

                var initialMode = loadViewMode('list');
                applyViewMode(initialMode);

                // Also re-map after small delay to ensure images/DOM ready
                setTimeout(function(){ try{ rebuildItemsMap(); rebuildChestsMap(); scheduleGridRender(0); }catch(_){ } }, 200);
            }catch(e){}
        })();

        // ================= UI: View mode toggle row =================
        (function(){
            try{
                var row = document.createElement('div'); row.style = rowCss;
                var label = document.createElement('div'); label.textContent = 'View:'; label.style = 'min-width:max-content; color:#2c4a8d; font-weight:700;';
                var btnList = document.createElement('button'); btnList.id='viewModeListBtn'; btnList.textContent='List'; btnList.style=pillBtnCss;
                var btnGrid = document.createElement('button'); btnGrid.id='viewModeGridBtn'; btnGrid.textContent='Grid'; btnGrid.style=pillBtnCss;

                function setActive(mode){ try{ if (mode==='grid'){ btnGrid.style.backgroundColor='#C5B0AE'; btnList.style.backgroundColor='#D2C0BE'; } else { btnList.style.backgroundColor='#C5B0AE'; btnGrid.style.backgroundColor='#D2C0BE'; } }catch(_){ } }
                async function save(mode){ try{ if (window.appConfig && window.appConfig.set) { await window.appConfig.set('promo_items_view_mode', mode); } else { try{ localStorage.setItem('promo_items_view_mode', mode); }catch(__){} } }catch(_){ } }
                async function load(){ try{ if (window.appConfig && window.appConfig.get){ var v = await window.appConfig.get('promo_items_view_mode', 'list'); return v||'list'; } try{ return localStorage.getItem('promo_items_view_mode')||'list'; }catch(__){ return 'list'; } }catch(_){ return 'list'; } }

                btnList.addEventListener('click', function(){ try{ save('list'); setActive('list'); if (window.__promoGrid_applyMode) window.__promoGrid_applyMode('list'); }catch(_){ } });
                btnGrid.addEventListener('click', function(){ try{ save('grid'); setActive('grid'); if (window.__promoGrid_applyMode) window.__promoGrid_applyMode('grid'); }catch(_){ } });

                setActive('list');
                try{ load().then(function(mode){ try{ setActive(mode); if (window.__promoGrid_applyMode) window.__promoGrid_applyMode(mode); }catch(__){} }); }catch(__){}
                row.appendChild(label); row.appendChild(btnList); row.appendChild(btnGrid);
                buttonContainer.appendChild(row);
            }catch(e){}
        })();

        // Section builders
        function mkRow(title){
            var row = document.createElement('div');
            row.style = rowCss;
            var lbl = document.createElement('div');
            lbl.innerText = title;
            lbl.style = 'min-width:120px; font-size:12px; font-weight:600; color:#705d4a;';
            row.appendChild(lbl);
            return { row: row, label: lbl };
        }
        function mkBtn(text, id, onClick){
            var b = document.createElement('button');
            b.innerText = text; if (id) b.id = id; b.style = pillBtnCss; b.onclick = function(){ try{ onClick && onClick(); }catch(e){} return false; };
            return b;
        }

        // Row 0: Global actions (in one row with label, like other sections)
        var rGlobal = mkRow('Quick Actions');
        rGlobal.row.append(selectAll);
        rGlobal.row.append(clearAll);

        var rAmu = mkRow('Consumables');
        try{ rAmu.label.title = 'Select or clear amulets, idols, royal cookies, and royal pies.'; }catch(e){}
        var selectAmulets = mkBtn('Select', 'selectAmuletsBtn', function(){
            selectByLabelTextRegexes([/платино.* амул.*/, /золот.* амул.*/, /серебр.* амул.*/, /бронзов.* амул.*/]);
            selectByLabelTextRegexes([/платино.* идол.*/, /золот.* идол.*/, /серебр.* идол.*/, /бронзов.* идол.*/]);
            selectByLabelTexts(['Королевское особое печенье', 'Королевские особые пирожки']);
        });
        var unselectAmulets = mkBtn('Clear', 'unselectAmuletsBtn', function(){
            unselectByLabelPatterns([/платино.* амул.*/, /золот.* амул.*/, /серебр.* амул.*/, /бронзов.* амул.*/]);
            unselectByLabelPatterns([/платино.* идол.*/, /золот.* идол.*/, /серебр.* идол.*/, /бронзов.* идол.*/]);
            unselectByLabelTexts(['Королевское особое печенье', 'Королевские особые пирожки']);
        });
        rAmu.row.append(selectAmulets);
        rAmu.row.append(unselectAmulets);

        var rPass = mkRow('Passes');
        try{ rPass.label.title = 'Select or clear Dream Gems.'; }catch(e){}
        var selectPass = mkBtn('Select', 'selectPassBtn', function(){ selectByLabelTexts(['Самоцвет грез']); });
        var unselectPass = mkBtn('Clear', 'unselectPassBtn', function(){ unselectByLabelTexts(['Самоцвет грез']); });
        rPass.row.append(selectPass);
        rPass.row.append(unselectPass);

        var rSub = mkRow('Subscription Gifts');
        try{ rSub.label.title = 'Select or clear subscription reward items.'; }catch(e){}
        var selectSubGifts = mkBtn('Select', 'selectSubGiftsBtn', function(){
            selectByLabelTexts([
                'Талон на золотой амулет',
                'Талон на золотого идола',
                'Дар из прошлого x2',
                'Королевское особое печенье x200',
                'Королевские особые пирожки x200'
            ]);
        });
        var removeSubGifts = mkBtn('Clear', 'removeSubGiftsBtn', function(){
            unselectByLabelTexts([
                'Талон на золотой амулет',
                'Талон на золотого идола',
                'Дар из прошлого x2',
                'Королевское особое печенье x200',
                'Королевские особые пирожки x200'
            ]);
        });
        rSub.row.append(selectSubGifts);
        rSub.row.append(removeSubGifts);

        var rDzPills = mkRow('Daily Quest Pills');
        try{ rDzPills.label.title = 'Select or clear Celestial Pills.'; }catch(e){}
        var selectDzPills = mkBtn('Select', 'selectDzPillsBtn', function(){
            selectByLabelTexts(['Небесная пилюля']);
        });
        var unselectDzPills = mkBtn('Clear', 'unselectDzPillsBtn', function(){
            unselectByLabelTexts(['Небесная пилюля']);
        });
        rDzPills.row.append(selectDzPills);
        rDzPills.row.append(unselectDzPills);

        // Custom search (separate block)
        var inputCustomSearch = document.createElement('input');
        inputCustomSearch.type = 'text';
        inputCustomSearch.id = 'customSearchInput';
        inputCustomSearch.placeholder = 'Enter an item name...';
        inputCustomSearch.style = 'padding:4px 8px; border-radius:10px; border:1px solid #C6B9A4; font-size:12px; line-height:18px; outline:none; flex:1; background:#FBF7EF; color:#333;';
        var selectCustom = mkBtn('Select', 'selectCustomBtn', function(){
            var query = (inputCustomSearch.value||'').trim();
            if (query.length>0) selectByLabelText(query);
        });
        var unselectCustom = mkBtn('Clear', 'unselectCustomBtn', function(){
            var query = (inputCustomSearch.value||'').trim();
            if (query.length>0) unselectByLabelText(query);
        });
        var customContainer = document.createElement('div');
        customContainer.style = 'display:flex; gap:6px; align-items:center;';
        customContainer.append(inputCustomSearch);
        customContainer.append(selectCustom);
        customContainer.append(unselectCustom);

        // Attach rows
        buttonContainer.append(rGlobal.row);
        var hr1 = document.createElement('div'); hr1.style='height:1px;background:#E2D8C9;margin:2px 0 3px 0;';
        buttonContainer.append(hr1);
        buttonContainer.append(rAmu.row);
        buttonContainer.append(rPass.row);
        buttonContainer.append(rSub.row);
        buttonContainer.append(rDzPills.row);
        var hr2 = document.createElement('div'); hr2.style='height:1px;background:#E2D8C9;margin:4px 0 3px 0;';
        buttonContainer.append(hr2);
        buttonContainer.append(customContainer);

        // Mount into popup content container
        var root = document.getElementById('promo_container');
        if (root) {
            root.append(buttonContainer);
            setTimeout(function(){ try{ scheduleAutosize(0); requestAnimationFrame(function(){ applyContentHeight(); }); }catch(e){} }, 0);
            try{
                if (!popup.getAttribute('data-mo-inited')){
                    var mo = new MutationObserver(function(){
                        var st0 = loadPopupState();
                        if (st0 && st0.collapsed) return;
                        scheduleAutosize(60);
                    });
                    mo.observe(root, { childList: true, subtree: true, attributes: false });
                    popup.setAttribute('data-mo-inited','1');
                }
            }catch(e){}
        }
    }
} catch(e) { /* ignore */ }
