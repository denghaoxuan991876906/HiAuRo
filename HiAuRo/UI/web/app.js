const OVERLAY_NAME = window.__OVERLAY_NAME__ || 'Default';
let ws;
let state = { enabled: false, paused: false, hotkeys: [], qts: [], job: '' };
let controls = [];
let activeTab = '';
let expanded = false;
let uiSettings = { qtCols:0, qtBtnW:0, qtVisible:{}, hkCols:0, hkBtnSize:52, hkVisible:{}, hkBindings:{} };

let _lastReportedSize = { width: 0, height: 0 };

function reportContentSize() {
    // MainWindow 固定尺寸，不自动调整窗口大小
    if (OVERLAY_NAME === 'MainWindow') return;
    // 用 root panel 的精确尺寸，避免 scrollWidth 包含 border 导致的累积误差
    const panel = document.querySelector('#main-win, #qt-panel, #hk-panel');
    const rect = panel ? panel.getBoundingClientRect() : document.documentElement.getBoundingClientRect();
    const w = Math.ceil(rect.width);
    const h = Math.ceil(rect.height);
    if (Math.abs(w - _lastReportedSize.width) < 5 &&
        Math.abs(h - _lastReportedSize.height) < 5) return;
    _lastReportedSize = { width: w, height: h };
    send('contentResize', {
        overlay: OVERLAY_NAME,
        width: Math.max(100, w),
        height: Math.max(50, h),
    });
}

// ================ WebSocket ================
function connect() {
    ws = new WebSocket('ws://localhost:5678/ws');
    ws.onclose = () => setTimeout(connect, 3000);
    ws.onerror = () => ws.close();
    ws.onmessage = (e) => {
        try {
            const msg = JSON.parse(e.data);
            switch (msg.type) {
                case 'status':
                    Object.assign(state, msg.data);
                    renderAll();
                    reportContentSize();
                    break;
                case 'acrState':
                    state.enabled = msg.data.enabled;
                    renderBarHeader();
                    renderExpandedActions();
                    break;
                case 'hotkeyExecuted':
                    flashHk(msg.data.id);
                    break;
                case 'qtChanged':
                    const qt = (state.qts || []).find(q => q.id === msg.data.id);
                    if (qt) qt.value = msg.data.value;
                    renderQt();
                    reportContentSize();
                    break;
                case 'pauseChanged':
                    state.paused = msg.data.paused;
                    renderBarHeader();
                    renderExpandedActions();
                    break;
                case 'controls':
                    controls = msg.data || [];
                    if (expanded) renderTabs();
                    reportContentSize();
                    break;
                case 'uiSettings':
                    uiSettings = Object.assign(uiSettings, msg.data);
                    if (expanded) renderTabs();
                    renderQt();
                    renderHk();
                    reportContentSize();
                    break;
            }
        } catch (ex) {}
    };
}

function send(type, data) {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ type, data: data || {} }));
    }
}

// ================ Bar Header ================
function renderBarHeader() {
    const dot = document.getElementById('status-dot');
    const label = document.getElementById('status-label');
    if (!dot || !label) return;
    const btnPause = document.getElementById('btn-pause');
    const btnAcr = document.getElementById('btn-acr');

    dot.className = 'status-dot';
    label.className = 'status-label';

    if (!state.enabled) {
        dot.classList.add('stopped');
        label.classList.add('stopped');
        label.textContent = '已停止';
        btnPause.style.display = 'none';
        btnAcr.innerHTML = iconPlay();
        btnAcr.title = '启动';
    } else if (state.paused) {
        dot.classList.add('paused');
        label.classList.add('paused');
        label.textContent = '已暂停';
        btnPause.style.display = '';
        btnPause.innerHTML = iconPlay();
        btnPause.title = '继续';
        btnAcr.innerHTML = iconStop();
        btnAcr.title = '停止';
    } else {
        dot.classList.add('running');
        label.classList.add('running');
        label.textContent = state.job || '运行中';
        btnPause.style.display = '';
        btnPause.innerHTML = iconPause();
        btnPause.title = '暂停';
        btnAcr.innerHTML = iconStop();
        btnAcr.title = '停止';
    }
}

function renderExpandedActions() {
    const btnAcr = document.getElementById('btn-acr-ex');
    const btnPause = document.getElementById('btn-pause-ex');
    if (!btnAcr || !btnPause) return;

    if (!state.enabled) {
        btnAcr.textContent = '启动'; btnAcr.className = 'btn-sm acr-off';
        btnPause.textContent = '暂停'; btnPause.className = 'btn-sm';
    } else if (state.paused) {
        btnAcr.textContent = '停止'; btnAcr.className = 'btn-sm acr-on';
        btnPause.textContent = '继续'; btnPause.className = 'btn-sm paused';
    } else {
        btnAcr.textContent = '停止'; btnAcr.className = 'btn-sm acr-on';
        btnPause.textContent = '暂停'; btnPause.className = 'btn-sm';
    }
}

function iconPlay() {
    return '<svg viewBox="0 0 24 24" fill="currentColor"><polygon points="5,3 19,12 5,21"/></svg>';
}
function iconPause() {
    return '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="4" width="4" height="16"/><rect x="14" y="4" width="4" height="16"/></svg>';
}
function iconStop() {
    return '<svg viewBox="0 0 24 24" fill="currentColor"><rect x="6" y="6" width="12" height="12" rx="1"/></svg>';
}

// ================ Qt chips ================
function renderQt() {
    const grid = document.getElementById('qt-grid');
    if (!grid) return;
    const qts = (state.qts || []).filter(q => uiSettings.qtVisible[q.id] !== false);
    grid.innerHTML = qts.map(q => 
        `<button class="qt-chip${q.value ? ' on' : ''}" title="${esc(q.tooltip || '')}" onclick="send('qttoggle',{id:'${esc(q.id)}'})">${esc(q.label)}</button>`
    ).join('');
    grid.style.setProperty('--qt-cols', uiSettings.qtCols || bestCols(qts.length));
}

// ================ Hotkey cells ================
function renderHk() {
    const grid = document.getElementById('hk-grid');
    if (!grid) return;
    const hks = (state.hotkeys || []).filter(h => uiSettings.hkVisible[h.id] !== false);
    grid.innerHTML = hks.map(h => 
        `<div class="hk-cell${h.available ? '' : ' unavailable'}" id="hk-${esc(h.id)}"
            title="${esc(h.label)}${h.binding ? ' [' + esc(h.binding) + ']' : ''}"
            onclick="${h.available ? `send('hotkey',{id:'${esc(h.id)}'})` : ''}">
            ${h.iconUrl ? `<img class="hk-sprite" src="${h.iconUrl}" alt="${esc(h.label)}">` : '<div class="hk-icon">⚡</div>'}
            <div class="hk-label">${esc(h.label)}</div>
            ${h.binding ? `<div class="hk-bind">${esc(h.binding)}</div>` : ''}
        </div>`
    ).join('');
    grid.style.setProperty('--hk-cols', uiSettings.hkCols || bestCols(hks.length));
}

function flashHk(id) {
    const el = document.getElementById('hk-' + id);
    if (!el) return;
    el.classList.add('flash');
    setTimeout(() => el.classList.remove('flash'), 600);
}

// ================ QT 设置 Tab ================
function renderQtSettings() {
    const body = document.getElementById('tab-body');
    if (!body) return;
    const qts = state.qts || [];
    let h = '<div class="set-group"><div class="set-group-title">布局</div>';
    h += `<div class="set-row"><span class="set-label">每行列数</span>
        <select class="set-select" onchange="uiSettings.qtCols=+this.value;saveUi();updateQtCols()">
        ${[2,3,4,5,6].map(c=>`<option value="${c}"${uiSettings.qtCols===c?' selected':''}>${c}</option>`).join('')}</select></div>`;
    h += '</div>';
    h += '<div class="set-group"><div class="set-group-title">显示/隐藏</div>';
    qts.forEach(q => {
        const vis = uiSettings.qtVisible[q.id] !== false;
        h += `<div class="set-row"><span class="set-label">${esc(q.label)}</span>
            <label class="ios-switch"><input type="checkbox"${vis?' checked':''} onchange="uiSettings.qtVisible['${esc(q.id)}']=this.checked;saveUi();renderQt()"><span class="track"></span></label></div>`;
    });
    h += '</div>';
    body.innerHTML = h;
    initCustomSelects(body);
}

// ================ 热键设置 Tab ================
function renderHkSettings() {
    const body = document.getElementById('tab-body');
    if (!body) return;
    const hks = state.hotkeys || [];
    let h = '<div class="set-group"><div class="set-group-title">布局</div>';
    h += `<div class="set-row"><span class="set-label">每行列数</span>
        <select class="set-select" onchange="uiSettings.hkCols=+this.value;saveUi();updateHkCols()">
        ${[2,3,4,5,6].map(c=>`<option value="${c}"${uiSettings.hkCols===c?' selected':''}>${c}</option>`).join('')}</select></div>`;
    h += '</div>';
    h += '<div class="set-group"><div class="set-group-title">显示/隐藏 + 快捷键</div>';
    hks.forEach(hk => {
        const vis = uiSettings.hkVisible[hk.id] !== false;
        const bind = uiSettings.hkBindings[hk.id] || '';
        h += `<div class="set-row"><span class="set-label">${esc(hk.label)}</span>
            <label class="ios-switch"><input type="checkbox"${vis?' checked':''} onchange="uiSettings.hkVisible['${esc(hk.id)}']=this.checked;saveUi();renderHk()"><span class="track"></span></label></div>`;
        h += `<div class="set-row" style="padding-left:8px"><span class="set-label" style="font-size:10px;color:var(--text-tertiary)">快捷键</span>
            <input class="set-input" value="${bind}" placeholder="如 F1" style="width:80px"
            onchange="setHkBinding('${esc(hk.id)}',this.value)"></div>`;
    });
    h += '</div>';
    body.innerHTML = h;
    initCustomSelects(body);
}

function setHkBinding(id, key) {
    uiSettings.hkBindings[id] = key;
    saveUi();
    send('setHkBinding', { id, key });
}

function saveUi() {
    send('saveUiSettings', uiSettings);
}


function updateQtCols() {
    const g = document.getElementById('qt-grid');
    if (g) g.style.setProperty('--qt-cols', uiSettings.qtCols);
}

function updateHkCols() {
    const g = document.getElementById('hk-grid');
    if (g) g.style.setProperty('--hk-cols', uiSettings.hkCols);
}

// ================ Expand / Collapse ================
function toggleExpand() {
    expanded = !expanded;
    const win = document.getElementById('main-win');
    const btn = document.getElementById('btn-expand');
    if (!win || !btn) return;

    if (expanded) {
        win.classList.add('expanded');
        btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="15 18 9 12 15 6"/></svg>';
        renderTabs();
    } else {
        win.classList.remove('expanded');
        btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="9 6 15 12 9 18"/></svg>';
    }
}

function renderTabs() {
    const tabBar = document.getElementById('tab-bar');
    if (!tabBar) return;
    const acrTabs = controls.filter(c => c.type === 'tab');
    const triggerTab = acrTabs.find(t => t.label === '触发器');
    const otherTabs = acrTabs.filter(t => t !== triggerTab);
    const allTabs = triggerTab
        ? [...otherTabs, triggerTab, { id: '__qt_set', label: 'QT设置' }, { id: '__hk_set', label: '热键设置' }]
        : [...otherTabs, { id: '__qt_set', label: 'QT设置' }, { id: '__hk_set', label: '热键设置' }];

    if (!allTabs.length) { tabBar.innerHTML = ''; return; }
    console.warn('[HiAuRo] renderTabs:', allTabs.length, 'tabs, activeTab:', activeTab);
    if (!activeTab || !allTabs.find(t => t.id === activeTab)) activeTab = allTabs[0].id;

    tabBar.innerHTML = allTabs.map(t =>
        `<button class="tab-btn${activeTab === t.id ? ' active' : ''}" onclick="switchTab('${esc(t.id)}')">${esc(t.label)}</button>`
    ).join('');

    if (activeTab === '__qt_set') renderQtSettings();
    else if (activeTab === '__hk_set') renderHkSettings();
    else renderTabBody();
}

function switchTab(tabId) {
    activeTab = tabId;
    renderTabs();
    reportContentSize();
}

function renderTabBody() {
    const body = document.getElementById('tab-body');
    if (!body) return;
    const tabControls = controls.filter(c => c.parentId === activeTab || (!c.parentId && c.type !== 'tab'));
    const groups = tabControls.filter(c => c.type === 'group');
    const orphaned = tabControls.filter(c => c.type !== 'group' && !groups.some(g => c.parentId === g.id));

    let html = '';
    for (const g of groups) {
        const items = controls.filter(c => c.parentId === g.id);
        html += `<div class="set-group"><div class="set-group-title">${esc(g.label)}</div>${renderItems(items)}</div>`;
    }
    html += renderItems(orphaned);
    body.innerHTML = html;
    initCustomSelects(body);
}

function renderItems(items) {
    let h = '';
    for (const c of items) {
        switch (c.type) {
            case 'checkbox':
                h += `<div class="set-row"><span class="set-label">${esc(c.label)}</span>` +
                    `<label class="ios-switch"><input type="checkbox"${c.value?' checked':''} id="ctl-${esc(c.id)}" onchange="ctlChanged('checkbox','${esc(c.id)}',this.checked)"><span class="track"></span></label></div>`;
                break;
            case 'slider':
                const min = c.options?.min ?? 0, max = c.options?.max ?? 100;
                h += `<div class="set-row"><span class="set-label">${esc(c.label)}</span>` +
                    `<input class="set-slider" type="range" min="${min}" max="${max}" value="${c.value}" step="1" oninput="document.getElementById('val-${esc(c.id)}').textContent=this.value" id="ctl-${esc(c.id)}">` +
                    `<span class="slider-val" id="val-${esc(c.id)}">${c.value}</span></div>`;
                break;
            case 'dropdown':
                const opts = (c.options||[]).map(o=>esc(o));
                h += `<div class="set-row"><span class="set-label">${esc(c.label)}</span>` +
                    `<select class="set-select" onchange="ctlChanged('dropdown','${esc(c.id)}',this.value)" id="ctl-${esc(c.id)}">` +
                    opts.map(o=>`<option value="${o}"${o===c.value?' selected':''}>${o}</option>`).join('')+'</select></div>';
                break;
            case 'intInput':
                h += `<div class="set-row"><span class="set-label">${esc(c.label)}</span>` +
                    `<input class="set-input" type="number" value="${c.value}" step="${c.meta?.step??1}" id="ctl-${esc(c.id)}" onchange="ctlChanged('intInput','${esc(c.id)}',this.value)"></div>`;
                break;
            case 'floatInput':
                h += `<div class="set-row"><span class="set-label">${esc(c.label)}</span>` +
                    `<input class="set-input" type="number" step="any" value="${c.value}" id="ctl-${esc(c.id)}" onchange="ctlChanged('floatInput','${esc(c.id)}',parseFloat(this.value))"></div>`;
                break;
            case 'textInput':
                h += `<div class="set-row"><span class="set-label">${esc(c.label)}</span>` +
                    `<input class="set-input" type="text" value="${esc(c.value)}" id="ctl-${esc(c.id)}" onchange="ctlChanged('textInput','${esc(c.id)}',this.value)"></div>`;
                break;
            case 'label':
                h += `<div class="set-label" style="padding:2px 0">${esc(c.value??c.label)}</div>`; break;
            case 'separator':
                h += '<div class="set-divider"></div>'; break;
        }
        const tip = items.find(i=>i.type==='tooltip'&&i.id==='__tip__'+c.id);
        if (tip) h += `<div style="font-size:10px;color:var(--text-tertiary);margin:0 0 2px 2px">${esc(tip.value)}</div>`;
    }
    return h;
}

function ctlChanged(type, id, value) {}

function initCustomSelects(container) {
    if (!container) return;
    const selects = container.querySelectorAll('select.set-select');
    selects.forEach(sel => {
        const wrapper = document.createElement('div');
        wrapper.className = 'custom-select';
        sel.parentNode.insertBefore(wrapper, sel);
        wrapper.appendChild(sel);
        const trigger = document.createElement('button');
        trigger.className = 'custom-select-trigger';
        trigger.textContent = sel.options[sel.selectedIndex]?.text || '';
        trigger.onclick = (e) => { e.stopPropagation(); wrapper.classList.toggle('open'); };
        wrapper.appendChild(trigger);
        const drop = document.createElement('div');
        drop.className = 'custom-select-drop';
        [...sel.options].forEach(opt => {
            const div = document.createElement('div');
            div.className = 'custom-select-opt' + (opt.selected ? ' selected' : '');
            div.textContent = opt.text;
            div.onclick = () => {
                sel.value = opt.value; trigger.textContent = opt.text;
                wrapper.classList.remove('open');
                sel.dispatchEvent(new Event('change', { bubbles: true }));
            };
            drop.appendChild(div);
        });
        wrapper.appendChild(drop);
    });
    document.onclick = (e) => {
        container.querySelectorAll('.custom-select.open').forEach(w => {
            if (!w.contains(e.target)) w.classList.remove('open');
        });
    };
}

function bestCols(n) {
    if (n <= 2) return n;
    let best = 2, be = n%2===0?0:1;
    for (const c of [3,4]) { const e = (c-n%c)%c; if (e<be||(e===be&&c>best)){best=c;be=e;} }
    return best;
}

function renderAll() {
    renderBarHeader();
    renderExpandedActions();
    renderQt();
    renderHk();
}

function esc(s) { return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

// ================ 事件绑定（替代 inline onclick，更可靠） ================
function bind(id, fn) {
    const el = document.getElementById(id);
    if (el) el.addEventListener('click', fn);
}
bind('btn-acr',    () => send('toggleACR'));
bind('btn-acr-ex', () => send('toggleACR'));
bind('btn-pause',    () => send('pause'));
bind('btn-pause-ex', () => send('pause'));
bind('btn-save',    () => send('saveACR'));
bind('btn-save-ex', () => send('saveACR'));
bind('btn-expand', toggleExpand);

connect();
