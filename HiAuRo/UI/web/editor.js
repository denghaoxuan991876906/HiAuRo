// ============================================================
// HiAuRo 三轴编辑器
// ============================================================

var currentAxis = 'execution';
var currentFile = '';
var fileHandle = null;
var timelineData = null;
var isDirty = false;
var selectedNodePath = null;
var contextMenuPath = null;
var globalDragBound = false;
var dragState = { active:false, srcPath:null, startX:0, startY:0, moved:false, preventClick:false };

var NODE_STYLES = {
    treeRoot:       { label:'Root',       color:'#30b0ff' },
    treeSequence:   { label:'序列',       color:'#30d158' },
    treeParallel:   { label:'并行',       color:'#ff9f0a' },
    treeSelect:     { label:'选择',       color:'#bf5af2' },
    treeLoop:       { label:'循环',       color:'#ff375f' },
    treeCondNode:   { label:'条件',       color:'#15aabf' },
    treeActionNode: { label:'动作',       color:'#f06595' },
    treeDelayNode:  { label:'延迟',       color:'#fcc419' },
    treeScriptNode: { label:'脚本',       color:'#4dabf7' },
    treePrintNode:  { label:'调试输出',   color:'#868e96' },
    treeClearWait:  { label:'清除等待',   color:'#495057' }
};

var ADD_TYPES = ['treeSequence','treeParallel','treeSelect','treeLoop','treeCondNode','treeActionNode','treeDelayNode','treeScriptNode','treePrintNode','treeClearWait'];

var localTriggers = { conditions: [], actions: [] };  // 本地导入的触发器

function getAllConditions() { return localTriggers.conditions || []; }
function getAllActions() { return localTriggers.actions || []; }

var dirHandle = null;
var fileEntries = [];

var factAxisData = null;
var factNodeTree = [];

// ==================== Cookie ====================

function setCookie(name, value, days) {
    var expires = '';
    if (days) { var d = new Date(); d.setTime(d.getTime() + days * 86400000); expires = '; expires=' + d.toUTCString(); }
    document.cookie = name + '=' + encodeURIComponent(value) + expires + '; path=/; SameSite=Lax';
}

function getCookie(name) {
    var match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
    return match ? decodeURIComponent(match[2]) : null;
}

// ==================== IndexedDB（持久化 FileSystemFileHandle） ====================

function dbOpen() {
    return new Promise(function(resolve, reject) {
        var req = indexedDB.open('HiAuRoEditor', 1);
        req.onupgradeneeded = function(e) { e.target.result.createObjectStore('handles'); };
        req.onsuccess = function(e) { resolve(e.target.result); };
        req.onerror = reject;
    });
}

function dbSaveCatalogHandle(handle) {
    return dbOpen().then(function(db) {
        return new Promise(function(resolve, reject) {
            var tx = db.transaction('handles', 'readwrite');
            tx.objectStore('handles').put(handle, 'catalogHandle');
            tx.oncomplete = function() { db.close(); resolve(); };
            tx.onerror = function() { db.close(); reject(tx.error); };
        });
    });
}

function dbLoadCatalogHandle() {
    return dbOpen().then(function(db) {
        return new Promise(function(resolve, reject) {
            var tx = db.transaction('handles', 'readonly');
            var r = tx.objectStore('handles').get('catalogHandle');
            r.onsuccess = function() { db.close(); resolve(r.result); };
            r.onerror = function() { db.close(); reject(r.error); };
        });
    }).catch(function() { return null; });
}

// ==================== 初始化 ====================

document.addEventListener('DOMContentLoaded', function() {
    document.querySelectorAll('.tab').forEach(function(btn) {
        btn.addEventListener('click', function() {
            if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
            document.querySelectorAll('.tab').forEach(function(b) { b.classList.remove('active'); });
            btn.classList.add('active');
            currentAxis = btn.dataset.axis;
            setCookie('hiAutoActiveAxis', currentAxis, 365);
            switchAxis();
        });
    });

    // 恢复 cookie 状态
    var savedAxis = getCookie('hiAutoActiveAxis');
    if (savedAxis && (savedAxis === 'execution' || savedAxis === 'assist')) {
        currentAxis = savedAxis;
        document.querySelectorAll('.tab').forEach(function(b) {
            b.classList.remove('active');
            if (b.dataset.axis === savedAxis) b.classList.add('active');
        });
    }
    document.getElementById('btnNew').addEventListener('click', newFile);
    document.getElementById('btnNewBig').addEventListener('click', newFile);
    document.getElementById('btnLoad').addEventListener('click', loadFile);
    document.getElementById('btnLoadBig').addEventListener('click', loadFile);
    document.getElementById('btnSave').addEventListener('click', saveFile);
    document.getElementById('btnSaveAs').addEventListener('click', saveFileAs);
    document.getElementById('btnExport').addEventListener('click', exportFile);
    document.getElementById('btnImport').addEventListener('click', function() { document.getElementById('edFileInput').click(); });
    document.getElementById('edFileInput').addEventListener('change', function() {
        var f = this.files[0]; if (f) { readFileObj(f); this.value = ''; }
    });
    document.getElementById('btnPickDir').addEventListener('click', pickDirectory);
    document.getElementById('factFileInput').addEventListener('change', function(e) {
        handleFactAxisLoaded(this.files[0]);
    });
    document.addEventListener('click', function(e) {
        if (e.target && e.target.closest('#btnLoadFactAxis')) loadFactAxisFile();
    });
    // 副本 ID 输入
    var tidEl = document.getElementById('territoryId');
    if (tidEl) {
        tidEl.addEventListener('change', function() {
            if (timelineData) { timelineData.TerritoryTypeId = parseInt(this.value) || 0; markDirty(); }
        });
    }
    var btnFactBar = document.getElementById('btnLoadFactAxisBar');
    if (btnFactBar) btnFactBar.addEventListener('click', loadFactAxisFile);
    // 左侧栏工具按钮
    var btnCat2 = document.getElementById('btnLoadCatalog2');
    if (btnCat2) btnCat2.addEventListener('click', loadCatalogFile);
    var btnFact2 = document.getElementById('btnLoadFactAxisBar2');
    if (btnFact2) btnFact2.addEventListener('click', loadFactAxisFile);
    // 快捷添加按钮
    document.querySelectorAll('.qbtn').forEach(function(btn) {
        btn.addEventListener('click', function() {
            if (!selectedNodePath) { setStatus('请先选中一个父节点','error'); return; }
            var parent = getNodeByPath(selectedNodePath);
            if (!parent) return;
            var type = detectType(parent);
            var isComp = ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
            if (!isComp) { setStatus('所选节点不支持添加子节点','error'); return; }
            addChildNode(selectedNodePath, this.dataset.add);
        });
    });
    document.addEventListener('keydown', function(e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 's') { e.preventDefault(); saveFile(); }
    });
    document.addEventListener('click', function(e) {
        hideContextMenu();
        if (!e.target.closest('.cust-select')) {
            document.querySelectorAll('.cust-select.open').forEach(function(w) { w.classList.remove('open'); });
        }
    });
    // 恢复本地触发器
    try {
        var saved = localStorage.getItem('hiAutoLocalTriggers');
        if (saved) localTriggers = JSON.parse(saved);
    } catch(e) { localTriggers = { conditions: [], actions: [] }; }

    // 导入本地触发器（左侧栏按钮）
    var btnTrig2 = document.getElementById('btnImportTrigger2');
    if (btnTrig2) {
        btnTrig2.addEventListener('click', function() {
            var json = prompt('粘贴触发器 JSON 定义（单条或数组）：\n格式：{ "typeName":"...", "aeTypeName":"...", "displayName":"触发名", "description":"描述", "parameters":[{ "name":"spellId", "type":"number", "defaultValue":0 }] }');
            if (!json) return;
            try {
                var items = JSON.parse(json);
                if (!Array.isArray(items)) items = [items];
                items.forEach(function(item) {
                    if (!item.typeName || !item.displayName) { setStatus('跳过无效项: 缺少 typeName 或 displayName', 'error'); return; }
                    item.category = 'local';
                    item.cloudSync = false;
                    var isCond = item.typeName.toLowerCase().indexOf('cond') >= 0;
                    var list = isCond ? localTriggers.conditions : localTriggers.actions;
                    var exists = list.some(function(t) { return t.typeName === item.typeName; });
                    if (!exists) list.push(item);
                });
                localStorage.setItem('hiAutoLocalTriggers', JSON.stringify(localTriggers));
                renderProps();
                setStatus('已导入 ' + items.length + ' 个触发器', 'success');
            } catch(e) {
                setStatus('JSON 解析失败: ' + e.message, 'error');
            }
        });
    }



    // 元数据字段绑定
    ['metaAuthor', 'metaVarDesc'].forEach(function(id) {
        var el = document.getElementById(id);
        if (!el) return;
        el.addEventListener('change', function() {
            if (!timelineData) return;
            if (id === 'metaAuthor') timelineData.Author = this.value;
            if (id === 'metaVarDesc') timelineData.ExposedVarDesc = this.value;
            markDirty();
        });
    });
    var jobEl = document.getElementById('metaJob');
    if (jobEl) jobEl.addEventListener('change', function() {
        if (timelineData) { timelineData.TargetJob = parseInt(this.value); markDirty(); }
    });
    var btnAddVar = document.getElementById('btnAddVar');
    if (btnAddVar) btnAddVar.addEventListener('click', function() {
        if (!timelineData) return;
        var vlist = timelineData.ExposedVars || timelineData.ExposedVarNames;
        if (!vlist) { vlist = []; timelineData.ExposedVars = vlist; }
        vlist.push('新变量');
        renderVarList();
        markDirty();
    });

    updateFooter();

    // 尝试自动恢复上次加载的触发器库
    restoreCatalogFromHandle();
});

// ==================== 轴切换 ====================

function switchAxis() {
    document.getElementById('treePanel').style.display = 'flex';
    document.getElementById('treeTools').style.display = '';
    selectedNodePath = null;
    renderTree();
    renderProps();
    updateFooter();
}

// ==================== 手写树 (执行轴) ====================

function renderTree() {
    var treeEl = document.getElementById('etree');
    var emptyEl = document.getElementById('emptyTree');
    if (!timelineData) {
        treeEl.style.display = 'none';
        emptyEl.style.display = '';
        document.getElementById('propsBody').innerHTML = '<div class="props-hint">点击节点查看属性</div>';
        return;
    }
    emptyEl.style.display = 'none';
    treeEl.style.display = '';
    var root = timelineData.TreeRoot || timelineData.treeRoot;
    treeEl.innerHTML = root ? buildTreeNodeHtml(root, '', 0) : '<div class="tn-row">空</div>';
    bindTreeClicks();
}

function detectType(node) {
    var full = (node['$type'] || '').split(',')[0];
    var tn = full.split('.').pop();
    var map = { TreeRoot:'treeRoot', TreeSequence:'treeSequence', TreeParallel:'treeParallel', TreeSelect:'treeSelect', TreeLoop:'treeLoop', TreeCondNode:'treeCondNode', TreeActionNode:'treeActionNode', TreeDelayNode:'treeDelayNode', TreeScriptNode:'treeScriptNode', TreePrintDebugInfoNode:'treePrintNode', TreeClearWaitNode:'treeClearWait' };
    if (map[tn]) return map[tn];
    if (node.TriggerConds) return 'treeCondNode';
    if (node.TriggerActions) return 'treeActionNode';
    if (node.Delay !== undefined) return 'treeDelayNode';
    if (node.Childs) return 'treeSequence';
    return 'treeSequence';
}

function typeToFull(type) {
    var map = {
        treeRoot: 'HiAuRo.Execution.TreeRoot, HiAuRo',
        treeSequence: 'HiAuRo.Execution.TreeSequence, HiAuRo',
        treeParallel: 'HiAuRo.Execution.TreeParallel, HiAuRo',
        treeSelect: 'HiAuRo.Execution.TreeSelect, HiAuRo',
        treeLoop: 'HiAuRo.Execution.TreeLoop, HiAuRo',
        treeCondNode: 'HiAuRo.Execution.TreeCondNode, HiAuRo',
        treeActionNode: 'HiAuRo.Execution.TreeActionNode, HiAuRo',
        treeDelayNode: 'HiAuRo.Execution.TreeDelayNode, HiAuRo',
        treeScriptNode: 'HiAuRo.Execution.TreeScriptNode, HiAuRo',
        treePrintNode: 'HiAuRo.Execution.TreePrintDebugInfoNode, HiAuRo',
        treeClearWait: 'HiAuRo.Execution.TreeClearWaitNode, HiAuRo'
    };
    return map[type] || type;
}

function newNodeDefaults(type) {
    var d = { '$type': typeToFull(type), DisplayName: NODE_STYLES[type].label, Id: Date.now(), Enable: true, Remark: '', Tag: '' };
    var isComp = ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
    if (isComp) d.Childs = [];
    if (type === 'treeLoop') d.Times = 1;
    if (type === 'treeDelayNode') d.Delay = 0;
    if (type === 'treeCondNode') { d.CheckOnce = false; d.ReverseResult = false; d.TriggerConds = []; }
    if (type === 'treeActionNode') d.TriggerActions = [];
    if (type === 'treeScriptNode') { d.Script = ''; d.OnlyCheck = false; d.FactNodeId = ''; }
    if (type === 'treeParallel') d.AnyReturn = false;
    if (type === 'treeSequence') d.IgnoreNodeResult = false;
    return d;
}

function buildTreeNodeHtml(node, path, depth) {
    var type = detectType(node);
    var st = NODE_STYLES[type] || NODE_STYLES.treeSequence;
    var label = node.DisplayName || node.name || st.label;
    var nodePath = path + (path ? '_' : '') + 'n';
    var selClass = nodePath === selectedNodePath ? ' sel' : '';
    var isComp = ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
    var children = node.Childs || node.childs || [];

    var h = '<div class="tn-block" data-path="' + nodePath + '">';
    h += '<div class="tn-row' + selClass + '" data-path="' + nodePath + '" data-type="' + type + '" style="border-left:4px solid ' + st.color + '">';
    h += '<span class="tn-tag" style="background:' + st.color + '">' + st.label + '</span>';
    h += '<span class="tn-name">' + esc(label) + '</span>';
    if (node.Tag) h += '<span class="tn-tag-label">#' + esc(node.Tag) + '</span>';
    if (node.Remark) h += '<span class="tn-remark">' + esc(node.Remark) + '</span>';
    if (isComp) h += '<span class="tn-child-count">' + children.length + ' 子</span>';
    if (isComp) h += '<button class="tn-add" data-add="' + nodePath + '" title="添加子节点">＋</button>';
    if (type !== 'treeRoot') h += '<button class="tn-del" data-del="' + nodePath + '" title="删除节点">✕</button>';
    h += '</div>';

    if (isComp && children.length > 0) {
        h += '<div class="tn-children">';
        children.forEach(function(child, i) {
            h += buildTreeNodeHtml(child, nodePath + '_' + i, depth + 1);
        });
        h += '</div>';
    }
    h += '</div>';
    return h;
}

function bindTreeClicks() {
    var tree = document.getElementById('etree');
    if (!tree) return;

    tree.querySelectorAll('.tn-row').forEach(function(row) {
        row.addEventListener('click', function(e) {
            if (dragState.preventClick) return; // 拖拽后忽略 click
            if (e.target.closest('.tn-add') || e.target.closest('.tn-del')) return;
            selectedNodePath = this.dataset.path;
            document.querySelectorAll('.tn-row').forEach(function(r) { r.classList.remove('sel'); });
            this.classList.add('sel');
            renderProps();
        });

        // 双击快捷操作
        row.addEventListener('dblclick', function(e) {
            if (e.target.closest('.tn-add') || e.target.closest('.tn-del')) return;
            var path = this.dataset.path;
            var node = getNodeByPath(path);
            if (!node) return;
            var type = detectType(node);
            var isComp = ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
            showQuickPopup(e, path, type, isComp);
        });

        row.addEventListener('contextmenu', function(e) {
            e.preventDefault();
            e.stopPropagation();
            showContextMenu(e, this.dataset.path);
        });

        // 手动拖拽 (左键按住 + 移动), 避免 HTML5 drag 干扰右键
        row.addEventListener('mousedown', function(e) {
            if (e.button !== 0) return;                     // 仅左键
            if (this.dataset.path === 'n') return;           // 根节点不可拖
            if (e.target.closest('.tn-add') || e.target.closest('.tn-del')) return;
            dragState.active = true;
            dragState.srcPath = this.dataset.path;
            dragState.startX = e.clientX;
            dragState.startY = e.clientY;
            dragState.moved = false;
        });
    });

    // 全局拖拽事件 (仅注册一次)
    if (!globalDragBound) {
        globalDragBound = true;

        document.addEventListener('mousemove', function(e) {
            if (!dragState.active || dragState.moved) return;
            var dx = Math.abs(e.clientX - dragState.startX);
            var dy = Math.abs(e.clientY - dragState.startY);
            if (dx + dy < 4) return;

            dragState.moved = true;
            document.body.classList.add('drag-active');
            var srcRow = document.querySelector('.tn-row[data-path="' + dragState.srcPath + '"]');
            if (srcRow) srcRow.classList.add('dragging');
        });

        document.addEventListener('mouseup', function(e) {
            if (!dragState.active) return;
            var srcPath = dragState.srcPath;
            var moved = dragState.moved;
            dragState.active = false;
            dragState.moved = false;
            document.body.classList.remove('drag-active');
            var srcRow = document.querySelector('.tn-row[data-path="' + srcPath + '"]');
            if (srcRow) srcRow.classList.remove('dragging');

            if (moved) {
                dragState.preventClick = true;
                setTimeout(function() { dragState.preventClick = false; }, 50);
                var target = document.elementFromPoint(e.clientX, e.clientY);
                var dstRow = target ? target.closest('.tn-row') : null;
                var dir = 'above';
                if (dstRow) {
                    if (dstRow.classList.contains('drop-child')) dir = 'child';
                    else if (dstRow.classList.contains('drop-below')) dir = 'below';
                }
                clearDropIndicators();
                if (!dstRow || dstRow.dataset.path === dragState.srcPath) return;
                // 防止拖入自己的后代
                if (dstRow.dataset.path.indexOf(dragState.srcPath + '_') === 0) return;
                moveNode(dragState.srcPath, dstRow.dataset.path, dir);
            }
        });

        // 拖拽过程中的放置指示器 (三区: 上边缘=前插, 中间复合节点=加入子, 下边缘=后插)
        tree.addEventListener('mousemove', function(e) {
            if (!dragState.moved) return;
            clearDropIndicators();
            var target = document.elementFromPoint(e.clientX, e.clientY);
            var row = target ? target.closest('.tn-row') : null;
            if (!row || row.dataset.path === dragState.srcPath) return;
            // 排除拖到自己的后代中
            if (row.dataset.path.indexOf(dragState.srcPath + '_') === 0) return;
            // Root 只能作为子节点添加
            if (row.dataset.path === 'n') { row.classList.add('drop-child'); return; }
            var rect = row.getBoundingClientRect();
            var zone = (e.clientY - rect.top) / rect.height;
            var type = row.dataset.type;
            var isComp = type && ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
            if (zone < 0.25) {
                row.classList.add('drop-above');
            } else if (zone > 0.75) {
                row.classList.add('drop-below');
            } else if (isComp) {
                row.classList.add('drop-child');
            } else {
                // 非复合节点中间区域: 仍按中线分上/下
                row.classList.add(zone < 0.5 ? 'drop-above' : 'drop-below');
            }
        });
    }

    tree.querySelectorAll('.tn-add').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.stopPropagation();
            showAddTypeMenu(e, this.dataset.add);
        });
    });

    tree.querySelectorAll('.tn-del').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.stopPropagation();
            if (!confirm('确定删除此节点？')) return;
            deleteNode(this.dataset.del);
        });
    });
}

function showContextMenu(e, path) {
    hideContextMenu();
    contextMenuPath = path;
    var node = getNodeByPath(path);
    if (!node) return;
    var type = detectType(node);
    var isComp = ['treeRoot','treeSequence','treeParallel','treeSelect','treeLoop'].indexOf(type) >= 0;
    var isRoot = type === 'treeRoot';

    var menu = document.createElement('div');
    menu.className = 'ctx-menu';
    menu.id = 'ctxMenu';
    menu.style.left = e.clientX + 'px';
    menu.style.top = e.clientY + 'px';

    if (isComp) {
        ADD_TYPES.forEach(function(ct) {
            var ns = NODE_STYLES[ct];
            var item = document.createElement('div');
            item.className = 'ctx-item';
            item.innerHTML = '<span class="ctx-dot" style="background:' + ns.color + '"></span>添加 ' + ns.label;
            item.addEventListener('click', function() { addChildNode(path, ct); });
            menu.appendChild(item);
        });
        var sep = document.createElement('div');
        sep.className = 'ctx-sep';
        menu.appendChild(sep);
    }

    if (!isRoot) {
        var upItem = document.createElement('div');
        upItem.className = 'ctx-item';
        upItem.textContent = '⬆ 上移';
        upItem.addEventListener('click', function() { moveSibling(path, -1); });
        menu.appendChild(upItem);

        var downItem = document.createElement('div');
        downItem.className = 'ctx-item';
        downItem.textContent = '⬇ 下移';
        downItem.addEventListener('click', function() { moveSibling(path, 1); });
        menu.appendChild(downItem);

        var sep2 = document.createElement('div');
        sep2.className = 'ctx-sep';
        menu.appendChild(sep2);

        var delItem = document.createElement('div');
        delItem.className = 'ctx-item ctx-danger';
        delItem.textContent = '✕ 删除';
        delItem.addEventListener('click', function() {
            if (confirm('确定删除此节点？')) deleteNode(path);
        });
        menu.appendChild(delItem);
    }

    document.body.appendChild(menu);
    e.stopPropagation();
}

function hideContextMenu() {
    var m = document.getElementById('ctxMenu');
    if (m && m.remove) m.remove();
    contextMenuPath = null;
}

// 快捷编辑字段渲染
function renderQuickField(ctrl, val) {
    var valStr = val !== undefined && val !== null ? String(val) : '';
    var div = document.createElement('div');
    div.style.cssText = 'display:flex;align-items:center;gap:4px;margin:2px 0';
    div.innerHTML = '<span style="font-size:10px;color:var(--tx2);min-width:60px">' + esc(ctrl.label) + '</span>';
    var input;
    if (ctrl.type === 'checkbox') {
        input = document.createElement('input');
        input.type = 'checkbox'; input.checked = !!val;
        input.style.cssText = 'margin:0';
    } else if (ctrl.type === 'dropdown') {
        input = document.createElement('select');
        input.style.cssText = 'flex:1;height:24px;font-size:11px;padding:1px 4px;border:1px solid var(--bd);background:var(--bg1);color:var(--tx1);border-radius:3px';
        (ctrl.options || []).forEach(function(o) {
            var opt = document.createElement('option');
            opt.value = o; opt.textContent = o;
            if (String(val) === String(o)) opt.selected = true;
            input.appendChild(opt);
        });
    } else {
        input = document.createElement('input');
        input.type = (ctrl.type === 'floatInput') ? 'number' : (ctrl.type === 'textInput' ? 'text' : 'number');
        if (ctrl.type === 'floatInput') input.step = 'any';
        if (ctrl.type === 'intInput') input.step = '1';
        input.value = valStr;
        input.style.cssText = 'flex:1;height:24px;font-size:11px;padding:1px 4px;border:1px solid var(--bd);background:var(--bg1);color:var(--tx1);border-radius:3px';
    }
    div.appendChild(input);
    return div;
}

// 双击弹窗
function showQuickPopup(e, path, type, isComp) {
    hideQuickPopup();
    var node = getNodeByPath(path);
    if (!node) return;

    var pop = document.createElement('div');
    pop.className = 'quick-popup';
    pop.id = 'quickPopup';

    if (isComp) {
        // 组合节点 → 快捷添加
        pop.innerHTML = '<div class="quick-title">快捷添加</div>';
        ADD_TYPES.forEach(function(ct) {
            var ns = NODE_STYLES[ct];
            var btn = document.createElement('div');
            btn.className = 'quick-item';
            btn.innerHTML = '<span class="ctx-dot" style="background:' + ns.color + '"></span>' + ns.label;
            btn.addEventListener('click', function() { addChildNode(path, ct); hideQuickPopup(); });
            pop.appendChild(btn);
        });
    } else {
        // 叶子节点 → 快捷编辑（只显示节点核心数据，不显示名称/备注等通用属性）
        pop.innerHTML = '<div class="quick-title">快捷编辑</div>';
        
        if (type === 'treeCondNode') {
            // 渲染条件列表
            var conds = node.TriggerConds || [];
            pop.innerHTML += '<div style="font-size:11px;color:var(--tx2);margin:4px 0">条件 (' + conds.length + ')</div>';
            conds.forEach(function(cond, i) {
                var ct = cond['$type'] || '';
                var entry = findCatalogEntry(ct, true);
                var name = entry ? entry.displayName : ct;
                var row = document.createElement('div');
                row.style.cssText = 'margin:3px 0;padding:4px;background:var(--bg1);border-radius:4px;font-size:11px';
                row.innerHTML = '<div style="color:#7af;margin-bottom:3px">' + esc(name) + '</div>';
                var ctrls = getEntryControls(entry);
                ctrls.forEach(function(ctrl) {
                    var val = cond[ctrl.label] !== undefined ? cond[ctrl.label] : ctrl.defaultValue;
                    var inp = renderQuickField(ctrl, val);
                    inp.addEventListener('change', function() {
                        if (this.type === 'checkbox') cond[ctrl.label] = this.checked;
                        else if (this.type === 'number') cond[ctrl.label] = parseFloat(this.value);
                        else cond[ctrl.label] = this.value;
                        markDirty();
                    });
                    row.appendChild(inp);
                });
                pop.appendChild(row);
            });
            // 添加条件按钮
            var addRow = document.createElement('div');
            addRow.style.cssText = 'margin-top:6px;display:flex;gap:4px';
            var allConds = getAllConditions();
            if (allConds.length) {
                var sel = document.createElement('select');
                sel.style.cssText = 'flex:1;height:24px;font-size:11px;border:1px solid var(--bd);background:var(--bg1);color:var(--tx1);border-radius:3px';
                sel.innerHTML = '<option value="">+ 添加条件</option>';
                allConds.forEach(function(c) { sel.innerHTML += '<option value="' + esc(c.typeDiscriminator || c.typeName) + '">' + esc(c.displayName || c.typeName) + '</option>'; });
                var addBtn = document.createElement('button');
                addBtn.textContent = '+'; addBtn.className = 'ed-add-btn';
                addBtn.addEventListener('click', function() {
                    if (!sel.value) return;
                    addTriggerCond(path, sel.value);
                    hideQuickPopup();
                    showQuickPopup({clientX: parseInt(pop.style.left), clientY: parseInt(pop.style.top)}, path, type, isComp);
                });
                addRow.appendChild(sel); addRow.appendChild(addBtn);
                pop.appendChild(addRow);
            }
        } else if (type === 'treeActionNode') {
            var acts = node.TriggerActions || [];
            pop.innerHTML += '<div style="font-size:11px;color:var(--tx2);margin:4px 0">动作 (' + acts.length + ')</div>';
            acts.forEach(function(act, i) {
                var at = act['$type'] || '';
                var entry = findCatalogEntry(at, false);
                var name = entry ? entry.displayName : at;
                var row = document.createElement('div');
                row.style.cssText = 'margin:3px 0;padding:4px;background:var(--bg1);border-radius:4px;font-size:11px';
                row.innerHTML = '<div style="color:#f06595;margin-bottom:3px">' + esc(name) + '</div>';
                var ctrls = getEntryControls(entry);
                ctrls.forEach(function(ctrl) {
                    var val = act[ctrl.label] !== undefined ? act[ctrl.label] : ctrl.defaultValue;
                    var inp = renderQuickField(ctrl, val);
                    inp.addEventListener('change', function() {
                        if (this.type === 'checkbox') act[ctrl.label] = this.checked;
                        else if (this.type === 'number') act[ctrl.label] = parseFloat(this.value);
                        else act[ctrl.label] = this.value;
                        markDirty();
                    });
                    row.appendChild(inp);
                });
                pop.appendChild(row);
            });
            // 添加动作按钮
            var addRowA = document.createElement('div');
            addRowA.style.cssText = 'margin-top:6px;display:flex;gap:4px';
            var allActs = getAllActions();
            if (allActs.length) {
                var selA = document.createElement('select');
                selA.style.cssText = 'flex:1;height:24px;font-size:11px;border:1px solid var(--bd);background:var(--bg1);color:var(--tx1);border-radius:3px';
                selA.innerHTML = '<option value="">+ 添加动作</option>';
                allActs.forEach(function(a) { selA.innerHTML += '<option value="' + esc(a.typeDiscriminator || a.typeName) + '">' + esc(a.displayName || a.typeName) + '</option>'; });
                var addBtnA = document.createElement('button');
                addBtnA.textContent = '+'; addBtnA.className = 'ed-add-btn';
                addBtnA.addEventListener('click', function() {
                    if (!selA.value) return;
                    addTriggerAction(path, selA.value);
                    hideQuickPopup();
                    showQuickPopup({clientX: parseInt(pop.style.left), clientY: parseInt(pop.style.top)}, path, type, isComp);
                });
                addRowA.appendChild(selA); addRowA.appendChild(addBtnA);
                pop.appendChild(addRowA);
            }
        } else if (type === 'treeDelayNode') {
            var dRow = document.createElement('div');
            dRow.style.cssText = 'display:flex;align-items:center;gap:6px;margin:4px 0';
            dRow.innerHTML = '<span style="font-size:12px;color:var(--tx2);min-width:36px">秒数</span>';
            var dIn = document.createElement('input');
            dIn.type = 'number'; dIn.step = 'any'; dIn.value = node.Delay || 0;
            dIn.style.cssText = 'flex:1;height:26px;font-size:12px;padding:2px 6px;border:1px solid var(--bd);background:var(--bg1);color:var(--tx1);border-radius:3px';
            dIn.addEventListener('change', function() { node.Delay = parseFloat(this.value) || 0; markDirty(); });
            dRow.appendChild(dIn); pop.appendChild(dRow);
        } else if (type === 'treeScriptNode') {
            var scriptIn = document.createElement('textarea');
            scriptIn.style.cssText = 'width:100%;height:80px;font-size:11px;font-family:monospace;padding:4px;border:1px solid var(--bd);background:var(--bg1);color:var(--tx1);border-radius:3px;resize:vertical';
            scriptIn.value = node.Script || '';
            scriptIn.addEventListener('change', function() { node.Script = this.value; markDirty(); });
            pop.appendChild(scriptIn);
            var factRow = document.createElement('div');
            factRow.style.cssText = 'margin:4px 0';
            factRow.innerHTML = '<span style="font-size:11px;color:var(--tx2)">FactNodeId: </span>';
            var factIn = document.createElement('input');
            factIn.type = 'text'; factIn.value = node.FactNodeId || '';
            factIn.style.cssText = 'width:100%;height:26px;font-size:12px;padding:2px 6px;border:1px solid var(--bd);background:var(--bg1);color:var(--tx1);border-radius:3px';
            factIn.addEventListener('change', function() { node.FactNodeId = this.value; markDirty(); });
            factRow.appendChild(factIn); pop.appendChild(factRow);
        } else if (type === 'treePrintNode') {
            var infoRow = document.createElement('div');
            infoRow.style.cssText = 'margin:4px 0';
            var infoIn = document.createElement('input');
            infoIn.type = 'text'; infoIn.value = node.Info || '';
            infoIn.placeholder = '调试信息';
            infoIn.style.cssText = 'width:100%;height:26px;font-size:12px;padding:2px 6px;border:1px solid var(--bd);background:var(--bg1);color:var(--tx1);border-radius:3px';
            infoIn.addEventListener('change', function() { node.Info = this.value; markDirty(); });
            infoRow.appendChild(infoIn); pop.appendChild(infoRow);
        }
    }

    // 定位在双击位置附近
    pop.style.left = (e.clientX + 10) + 'px';
    pop.style.top = (e.clientY - 10) + 'px';
    // 防止超出右边界
    setTimeout(function() {
        var r = pop.getBoundingClientRect();
        if (r.right > window.innerWidth) pop.style.left = (window.innerWidth - r.width - 20) + 'px';
        if (r.bottom > window.innerHeight) pop.style.top = (window.innerHeight - r.height - 20) + 'px';
    }, 0);

    document.body.appendChild(pop);
    e.stopPropagation();
}

function hideQuickPopup() {
    var p = document.getElementById('quickPopup');
    if (p) p.remove();
}

// 点击空白处关闭弹窗
document.addEventListener('click', function(e) {
    var pop = document.getElementById('quickPopup');
    if (pop && !pop.contains(e.target)) hideQuickPopup();
});

function clearDropIndicators() {
    document.querySelectorAll('.tn-row.drop-above, .tn-row.drop-below, .tn-row.drop-child').forEach(function(r) {
        r.classList.remove('drop-above', 'drop-below', 'drop-child');
    });
}

function showAddTypeMenu(e, parentPath) {
    hideContextMenu();
    var menu = document.createElement('div');
    menu.className = 'ctx-menu';
    menu.id = 'ctxMenu';
    var rect = e.target.getBoundingClientRect();
    menu.style.left = rect.left + 'px';
    menu.style.top = (rect.bottom + 4) + 'px';

    ADD_TYPES.forEach(function(ct) {
        var ns = NODE_STYLES[ct];
        var item = document.createElement('div');
        item.className = 'ctx-item';
        item.innerHTML = '<span class="ctx-dot" style="background:' + ns.color + '"></span>' + ns.label;
        item.addEventListener('click', function() { addChildNode(parentPath, ct); });
        menu.appendChild(item);
    });

    document.body.appendChild(menu);
    e.stopPropagation();
}

function getNodeByPath(path) {
    if (!path || !timelineData) return null;
    var root = timelineData.TreeRoot || timelineData.treeRoot;
    if (!root) return null;
    var parts = path.split('_');
    var obj = root;
    for (var i = 0; i < parts.length; i++) {
        var idx = parseInt(parts[i]);
        if (isNaN(idx)) continue;
        var children = obj.Childs || obj.childs || [];
        if (idx >= children.length) return null;
        obj = children[idx];
    }
    return obj;
}

function getParentInfo(path) {
    // path 格式: n / n_0_n / n_1_n / n_0_n_0_n (n标记与数字索引交替)
    // 最后一个 _n 是节点标记, 倒数第二个是它在父级 children 中的索引
    if (!path) return null;
    var parts = path.split('_');
    if (parts.length < 2) {
        // 根节点 n: 无父级
        var root = timelineData && (timelineData.TreeRoot || timelineData.treeRoot);
        return { node: root, children: root ? (root.Childs || root.childs || []) : [], idx: -1 };
    }
    var idx = parseInt(parts[parts.length - 2]);  // 倒数第二个是数字索引
    var parentPath = parts.slice(0, -2).join('_'); // 去掉最后两段得到父路径
    var parent = getNodeByPath(parentPath) || (timelineData && (timelineData.TreeRoot || timelineData.treeRoot));
    if (!parent) return null;
    return { node: parent, children: parent.Childs || parent.childs || [], idx: idx };
}

function addChildNode(parentPath, type) {
    var parent = getNodeByPath(parentPath);
    if (!parent) return;
    if (!parent.Childs) parent.Childs = [];
    parent.Childs.push(newNodeDefaults(type));
    hideContextMenu();
    markDirty();
}

function deleteNode(path) {
    var p = getParentInfo(path);
    if (!p || !p.children || p.idx < 0) return;
    p.children.splice(p.idx, 1);
    if (selectedNodePath === path || (selectedNodePath||'').indexOf(path + '_') === 0) selectedNodePath = null;
    markDirty();
}

function moveNode(srcPath, dstPath, dir) {
    var srcNode = getNodeByPath(srcPath);
    var dstNode = getNodeByPath(dstPath);
    if (!srcNode || !dstNode) return;
    // 防止拖入自己的后代
    if (dstPath.indexOf(srcPath + '_') === 0) return;

    if (dir === 'child') {
        // 作为子节点添加到目标
        var si = getParentInfo(srcPath);
        if (!si || si.idx < 0 || si.idx >= si.children.length) return;
        if (!dstNode.Childs) dstNode.Childs = [];
        si.children.splice(si.idx, 1);
        dstNode.Childs.push(srcNode);
        markDirty();
        return;
    }

    // sibling 插入 (above / below)
    var si = getParentInfo(srcPath);
    var di = getParentInfo(dstPath);
    if (!si || !di || !si.children || !di.children) return;
    if (si.idx < 0 || si.idx >= si.children.length) return;
    if (di.idx < 0 || di.idx >= di.children.length) return;

    var sameParent = si.children === di.children;
    if (sameParent && si.idx === di.idx) return;

    var item = si.children.splice(si.idx, 1)[0];
    var insertIdx;
    if (sameParent) {
        // 同父级: 源移除后目标索引可能左移
        insertIdx = dir === 'below'
            ? (si.idx < di.idx ? di.idx : di.idx + 1)
            : (si.idx < di.idx ? di.idx - 1 : di.idx);
    } else {
        // 跨父级: di.children 不受源移除影响
        insertIdx = dir === 'below' ? di.idx + 1 : di.idx;
    }
    di.children.splice(insertIdx, 0, item);
    markDirty();
}

function moveSibling(path, delta) {
    var p = getParentInfo(path);
    if (!p || !p.children) return;
    if (p.idx < 0 || p.idx >= p.children.length) return;
    var newIdx = p.idx + delta;
    if (newIdx < 0 || newIdx >= p.children.length) return;
    var item = p.children.splice(p.idx, 1)[0];
    p.children.splice(newIdx, 0, item);
    hideContextMenu();
    markDirty();
}

// ==================== 触发器参数辅助 ====================

function findCatalogEntry(typeName, isCond) {
    var list = isCond ? getAllConditions() : getAllActions();
    return list.find(function(e) { return (e.aeTypeName || e.typeName) === typeName; });
}

function getEntryControls(entry) {
    if (!entry) return [];
    if (entry.controls && entry.controls.length) return entry.controls;
    if (entry.parameters && entry.parameters.length) {
        return entry.parameters.map(function(p) {
            return { label: p.name, type: p.type || 'textInput', defaultValue: p.defaultValue, options: p.options || null };
        });
    }
    return [];
}

function renderTriggerField(ctrl, val, idx, kind) {
    var idxAttr = kind === 'cond' ? 'data-cond-idx' : 'data-act-idx';
    var valStr = val !== undefined && val !== null ? String(val) : '';
    switch (ctrl.type) {
        case 'intInput':
            return '<div class="trigger-field"><label>' + esc(ctrl.label) + '</label>' +
                '<input type="number" step="1" class="trigger-param-input" ' + idxAttr + '="' + idx + '" data-field="' + esc(ctrl.label) + '" value="' + esc(valStr) + '" /></div>';
        case 'floatInput':
            return '<div class="trigger-field"><label>' + esc(ctrl.label) + '</label>' +
                '<input type="number" step="any" class="trigger-param-input" ' + idxAttr + '="' + idx + '" data-field="' + esc(ctrl.label) + '" value="' + esc(valStr) + '" /></div>';
        case 'textInput':
            return '<div class="trigger-field"><label>' + esc(ctrl.label) + '</label>' +
                '<input type="text" class="trigger-param-input" ' + idxAttr + '="' + idx + '" data-field="' + esc(ctrl.label) + '" value="' + esc(valStr) + '" /></div>';
        case 'checkbox':
            return '<div class="trigger-field"><label><input type="checkbox" class="trigger-param-input" ' + idxAttr + '="' + idx + '" data-field="' + esc(ctrl.label) + '" ' + (val ? 'checked' : '') + ' /> ' + esc(ctrl.label) + '</label></div>';
        case 'dropdown':
            var opts = ctrl.options || [];
            var html = '<div class="trigger-field"><label>' + esc(ctrl.label) + '</label>' +
                '<select class="trigger-param-input" ' + idxAttr + '="' + idx + '" data-field="' + esc(ctrl.label) + '">';
            opts.forEach(function(o) {
                html += '<option value="' + esc(o) + '"' + (String(val) === String(o) ? ' selected' : '') + '>' + esc(o) + '</option>';
            });
            html += '</select></div>';
            return html;
        default:
            return '<div class="trigger-field"><label>' + esc(ctrl.label) + '</label>' +
                '<input type="text" class="trigger-param-input" ' + idxAttr + '="' + idx + '" data-field="' + esc(ctrl.label) + '" value="' + esc(valStr) + '" /></div>';
    }
}

// ==================== 属性面板 ====================

function renderProps() {
    var body = document.getElementById('propsBody');
    var hdr = document.getElementById('propsLabel');

    if (selectedNodePath && timelineData) {
        var node = getNodeByPath(selectedNodePath);
        if (!node) { body.innerHTML = '<p class="props-hint">节点不存在</p>'; return; }
        var type = detectType(node);
        hdr.textContent = '节点: ' + (node.DisplayName || NODE_STYLES[type].label);
        var h = '<div class="ed-prop-section"><div class="ed-prop-head">基础</div>';
        h += prop('名称', 'text', node, 'DisplayName');
        h += prop('ID', 'number', node, 'Id');
        h += prop('启用', 'checkbox', node, 'Enable');
        h += prop('备注', 'text', node, 'Remark');
        h += prop('标签', 'text', node, 'Tag');
        h += '</div>';
        if (type === 'treeCondNode') {
            h += '<div class="ed-prop-section"><div class="ed-prop-head">条件</div>';
            h += prop('检查一次', 'checkbox', node, 'CheckOnce');
            h += '<div class="ed-prop-row"><span class="ed-prop-label">条件逻辑</span>';
            h += '<select class="ed-prop-input" id="prop_CondLogicType" data-key="CondLogicType">';
            h += '<option value="0" ' + (node.CondLogicType === 1 ? '' : 'selected') + '>And (全部满足)</option>';
            h += '<option value="1" ' + (node.CondLogicType === 1 ? 'selected' : '') + '>Or (任一满足)</option>';
            h += '</select></div>';
            h += prop('结果取反', 'checkbox', node, 'ReverseResult');
            h += '<div class="prop-group">';
            h += '<label>条件列表 (' + (node.TriggerConds||[]).length + ' 项)</label>';
            var conds = getAllConditions();
            if (conds.length > 0) {
                var condOpts = conds.map(function(c) { return { value: c.aeTypeName || c.typeName, label: (c.displayName || c.aeTypeName) + ' [' + (c.category||'') + ']' }; });
                h += iosSelect([{value:'',label:'+ 添加条件...'}].concat(condOpts), '',
                    'addTriggerCond(\'' + selectedNodePath + '\', this.dataset.val)');
            } else {
                h += '<div class="prop-hint">无可用条件（请导入或连接插件）</div>';
            }
            (node.TriggerConds||[]).forEach(function(cond, idx) {
                var ct = cond['$type'] || cond.aeTypeName || '';
                var entry = findCatalogEntry(ct, true);
                var displayName = entry ? (entry.displayName || ct) : ct;
                var ctrls = getEntryControls(entry);
                h += '<div class="trigger-cond-item">';
                h += '<div class="trigger-cond-header"><span>' + esc(displayName) + '</span>';
                h += '<button class="ed-del-btn" onclick="removeTriggerCond(\'' + selectedNodePath + '\',' + idx + ')">✕</button></div>';
                ctrls.forEach(function(ctrl) {
                    var val = cond[ctrl.label] !== undefined ? cond[ctrl.label] : (ctrl.defaultValue !== undefined ? ctrl.defaultValue : '');
                    h += renderTriggerField(ctrl, val, idx, 'cond');
                });
                h += '</div>';
            });
            h += '</div>';
            h += '</div>';
        }
        if (type === 'treeActionNode') {
            h += '<div class="ed-prop-section"><div class="ed-prop-head">动作</div>';
            h += '<div class="prop-group">';
            h += '<label>动作列表 (' + (node.TriggerActions||[]).length + ' 项)</label>';
            var actions = getAllActions();
            if (actions.length > 0) {
                var actOpts = actions.map(function(a) { return { value: a.aeTypeName || a.typeName, label: (a.displayName || a.aeTypeName) + ' [' + (a.category||'') + ']' }; });
                h += iosSelect([{value:'',label:'+ 添加动作...'}].concat(actOpts), '',
                    'addTriggerAction(\'' + selectedNodePath + '\', this.dataset.val)');
            } else {
                h += '<div class="prop-hint">无可用动作</div>';
            }
            (node.TriggerActions||[]).forEach(function(act, idx) {
                var at = act['$type'] || act.aeTypeName || '';
                var entry = findCatalogEntry(at, false);
                var displayName = entry ? (entry.displayName || at) : at;
                var ctrls = getEntryControls(entry);
                h += '<div class="trigger-cond-item">';
                h += '<div class="trigger-cond-header"><span>' + esc(displayName) + '</span>';
                h += '<button class="ed-del-btn" onclick="removeTriggerAction(\'' + selectedNodePath + '\',' + idx + ')">✕</button></div>';
                ctrls.forEach(function(ctrl) {
                    var val = act[ctrl.label] !== undefined ? act[ctrl.label] : (ctrl.defaultValue !== undefined ? ctrl.defaultValue : '');
                    h += renderTriggerField(ctrl, val, idx, 'act');
                });
                h += '</div>';
            });
            h += '</div>';
            h += '</div>';
        }
        if (type === 'treeDelayNode') { h += '<div class="ed-prop-section"><div class="ed-prop-head">延迟</div>'; h += prop('秒数', 'number', node, 'Delay'); h += '</div>'; }
        if (type === 'treeLoop') { h += '<div class="ed-prop-section"><div class="ed-prop-head">循环</div>'; h += prop('次数', 'number', node, 'Times'); h += '</div>'; }
        if (type === 'treeScriptNode') { h += '<div class="ed-prop-section"><div class="ed-prop-head">脚本</div>'; h += prop('仅检查(不等待)', 'checkbox', node, 'OnlyCheck');
            if (factNodeTree.length > 0) {
                h += '<div class="ed-prop-row"><span class="ed-prop-label">事实轴节点</span>';
                h += '<select class="ed-prop-input" id="prop_FactNodeId" data-key="FactNodeId">';
                h += '<option value="">\u2014 \u672a\u7ed1\u5b9a \u2014</option>';
                factNodeTree.forEach(function(n) {
                    h += '<option value="' + esc(n.id) + '"' + (node.FactNodeId === n.id ? ' selected' : '') + '>' + esc(n.label) + '</option>';
                });
                h += '</select></div>';
            } else {
                h += prop('\u4e8b\u5b9e\u8f74\u8282\u70b9 ID', 'text', node, 'FactNodeId');
            }
            h += '<div class="ed-prop-row"><button class="btn-sm" style="margin-top:2px" id="btnLoadFactAxis" type="button">\ud83d\udcc2 \u52a0\u8f7d\u4e8b\u5b9e\u8f74</button></div>';
            h += '<textarea class="ed-prop-area" id="dfScript" style="width:100%;height:80px;font-size:11px;font-family:monospace">'+esc(node.Script||'')+'</textarea>'; h += '<button class="btn-sm" style="margin-top:4px" onclick="saveTreeScript()">保存脚本</button>'; h += '</div>'; }
        body.innerHTML = h;
        bindTreePropInputs();
        bindTriggerParamInputs(node);
        return;
    }

    hdr.textContent = '属性'; body.innerHTML = '<p class="props-hint">点击节点查看属性</p>';
}

function prop(label, type, obj, key) {
    var v = obj[key] !== undefined ? obj[key] : (type==='checkbox'?false:'');
    var id = 'p_'+Math.random().toString(36).substr(2,8);
    if (type === 'checkbox')
        return '<div class="ed-prop-row"><span class="ed-prop-label">'+label+'</span><input class="ed-prop-input" type="checkbox" id="'+id+'" data-key="'+key+'" '+(v?'checked':'')+'></div>';
    return '<div class="ed-prop-row"><span class="ed-prop-label">'+label+'</span><input class="ed-prop-input" type="'+type+'" id="'+id+'" data-key="'+key+'" value="'+esc(String(v))+'"></div>';
}

function bindTreePropInputs() {
    document.querySelectorAll('#edPropsBody .ed-prop-input').forEach(function(el) {
        if (el.dataset.bound) return; el.dataset.bound = '1';
        el.addEventListener('change', function() {
            var node = getNodeByPath(selectedNodePath);
            if (!node) return;
            var key = this.dataset.key;
            node[key] = this.type==='checkbox' ? this.checked : (this.type==='number' ? Number(this.value) : this.value);
            markDirty();
        });
    });
}

function bindTriggerParamInputs(node) {
    document.querySelectorAll('.trigger-param-input').forEach(function(el) {
        if (el.dataset.bound) return; el.dataset.bound = '1';
        el.addEventListener('change', function() {
            var field = this.dataset.field;
            var isCond = this.hasAttribute('data-cond-idx');
            var idx = parseInt(isCond ? this.dataset.condIdx : this.dataset.actIdx);
            var list = isCond ? node.TriggerConds : node.TriggerActions;
            if (!list || !list[idx]) return;
            var inst = list[idx];
            if (this.type === 'checkbox')
                inst[field] = this.checked;
            else if (this.type === 'number')
                inst[field] = parseFloat(this.value);
            else
                inst[field] = this.value;
            markDirty();
        });
    });
}

function saveTreeScript() {
    var node = getNodeByPath(selectedNodePath);
    var ta = document.getElementById('dfScript');
    if (node && ta) { node.Script = ta.value; markDirty(); setStatus('脚本已保存'); }
}

// ==================== 事实轴加载 ====================

function loadFactAxisFile() {
    var input = document.getElementById('factFileInput');
    if (input) input.click();
}

function handleFactAxisLoaded(file) {
    if (!file) return;
    var reader = new FileReader();
    reader.onload = function() {
        try {
            factAxisData = JSON.parse(reader.result);
            buildFactNodeTree();
            renderProps();
        } catch(e) {
            console.log('Fact axis parse error:', e);
        }
    };
    reader.readAsText(file);
}

function buildFactNodeTree() {
    factNodeTree = [];
    if (!factAxisData || !factAxisData.phases) return;
    factAxisData.phases.forEach(function(phase) {
        factNodeTree.push({ id: phase.id, label: '\u25b8 ' + esc(phase.name || phase.id), type: 'phase' });
        (phase.events || []).forEach(function(ev) {
            factNodeTree.push({ id: ev.id, label: '  \u25ce ' + esc(ev.name || ev.id) + ' (' + (ev.time||0) + 's)', type: 'event' });
        });
        if (phase.switch && phase.switch.branches) {
            phase.switch.branches.forEach(function(br, i) {
                factNodeTree.push({ id: phase.id + '#branch' + i, label: '  \u25c7 ' + esc(br.name || ('Branch' + i)), type: 'branch' });
            });
        }
    });
}

// ==================== 文件浏览器 ====================

async function pickDirectory() {
    if (!window.showDirectoryPicker) {
        setStatus('浏览器不支持目录选择','error'); return;
    }
    try {
        dirHandle = await window.showDirectoryPicker();
        document.getElementById('fileDir').textContent = dirHandle.name;
        setCookie('hiAutoTimelineDir', dirHandle.name, 365);
        refreshFileList();
    } catch(e) { if (e.name !== 'AbortError') setStatus('目录选择失败','error'); }
}

async function refreshFileList() {
    var list = document.getElementById('fileList');
    if (!dirHandle) { list.innerHTML = '<div class="props-hint">点"选择目录"浏览文件</div>'; return; }

    fileEntries = [];
    var ext = '.json';
    for await (var [name, handle] of dirHandle.entries()) {
        if (handle.kind === 'file' && name.endsWith(ext)) {
            fileEntries.push({ name: name, handle: handle });
        }
    }
    fileEntries.sort(function(a,b) { return a.name.localeCompare(b.name); });

    if (fileEntries.length === 0) {
        list.innerHTML = '<div class="props-hint">目录中无 .json 文件</div>';
        return;
    }

    var h = '';
    fileEntries.forEach(function(entry) {
        var cls = entry.name === currentFile ? ' active' : '';
        h += '<div class="file-item' + cls + '" data-file="' + esc(entry.name) + '">';
        h += '<span class="file-name">' + esc(entry.name) + '</span>';
        h += '</div>';
    });
    list.innerHTML = h;

    list.querySelectorAll('.file-item').forEach(function(item) {
        item.addEventListener('click', function() {
            if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
            openFromDir(this.dataset.file);
        });
    });
}

async function openFromDir(fileName) {
    var entry = fileEntries.find(function(e) { return e.name === fileName; });
    if (!entry) return;
    try {
        var file = await entry.handle.getFile();
        fileHandle = entry.handle;
        readFileObj(file);
    } catch(e) { setStatus('打开失败: ' + e.message, 'error'); }
}

// ==================== 文件操作 ====================

function newFile() {
    if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    timelineData = { Name:'新执行轴', TerritoryTypeId:0, Note:'', Guid:generateUUID(), ExposedVars:[], TreeRoot:{ '$type':'HiAuRo.Execution.TreeRoot, HiAuRo', DisplayName:'Root', Id:1, Enable:true, Remark:'', Tag:'', Childs:[] } };
    currentFile=''; fileHandle=null; isDirty=true; selectedNodePath=null;
    switchAxis(); setStatus('已新建');
}
async function loadFile() {
    if (isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    if (window.showOpenFilePicker) {
        try { var handles = await window.showOpenFilePicker({ types:[{ description:'JSON', accept:{'application/json':['.json','.txt']} }] }); var file = await handles[0].getFile(); fileHandle = handles[0]; readFileObj(file); } catch(e) {}
    } else document.getElementById('edFileInput').click();
}
function readFileObj(file) {
    var r = new FileReader();
        r.onload = function() { try { timelineData = JSON.parse(r.result); } catch(ex) { setStatus('JSON解析失败: '+ex.message,'error'); return; } currentFile=file.name; isDirty=false; selectedNodePath=null; switchAxis(); syncMeta(); setStatus('已加载: '+file.name,'success'); };
    r.readAsText(file);
}
async function saveFile() {
    if (!timelineData) { setStatus('无内容','error'); return; }
    var json = JSON.stringify(timelineData, null, 2);
    if (fileHandle && typeof fileHandle.createWritable === 'function') { try { var w = await fileHandle.createWritable(); await w.write(json); await w.close(); isDirty=false; updateFooter(); setStatus('已保存: '+currentFile,'success'); return; } catch(e) {} }
    saveFileAs();
}
async function saveFileAs() {
    if (!timelineData) { setStatus('无内容','error'); return; }
    var json = JSON.stringify(timelineData, null, 2); var name = currentFile || 'timeline.json';
    if (window.showSaveFilePicker) { try { var h = await window.showSaveFilePicker({ suggestedName:name, types:[{ description:'JSON', accept:{'application/json':['.json']} }] }); var w = await h.createWritable(); await w.write(json); await w.close(); fileHandle=h; currentFile=h.name; isDirty=false; updateFooter(); setStatus('已保存: '+currentFile,'success'); return; } catch(e) { if(e.name==='AbortError')return; } }
    downloadJson(json, name);
}
function exportFile() { if (!timelineData) { setStatus('无内容','error'); return; } downloadJson(JSON.stringify(timelineData,null,2), currentFile||'export.json'); setStatus('已导出'); }
function downloadJson(json, name) { var b=new Blob([json],{type:'application/json'}); var u=URL.createObjectURL(b); var a=document.createElement('a'); a.href=u; a.download=name; a.click(); URL.revokeObjectURL(u); }
// ==================== 目录文件加载 ====================

async function loadCatalogFile() {
    if (window.showOpenFilePicker) {
        try {
            var handles = await window.showOpenFilePicker({ types: [{ description: 'JSON', accept: { 'application/json': ['.json'] } }] });
            var handle = handles[0];
            var file = await handle.getFile();
            processCatalog(file);
            await dbSaveCatalogHandle(handle);
            setCookie('hiAutoCatalogFile', file.name, 365);
            return;
        } catch(e) { if (e.name === 'AbortError') return; }
    }
    // 降级：老式 <input type="file">
    var input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = function() {
        var file = this.files[0];
        if (!file) return;
        processCatalog(file);
        setCookie('hiAutoCatalogFile', file.name, 365);
    };
    input.click();
}

function processCatalog(file) {
    var reader = new FileReader();
    reader.onload = function() {
        try {
            var catalog = JSON.parse(reader.result);
            var conds = catalog.conditions || [];
            var acts = catalog.actions || [];
            conds.forEach(function(c) { c.category = 'catalog'; });
            acts.forEach(function(a) { a.category = 'catalog'; });
            localTriggers.conditions = conds.concat(localTriggers.conditions || []);
            localTriggers.actions = acts.concat(localTriggers.actions || []);
            localStorage.setItem('hiAutoLocalTriggers', JSON.stringify(localTriggers));
            setStatus('已加载触发器库: ' + conds.length + ' 条件, ' + acts.length + ' 动作', 'success');
            renderProps();
        } catch(e) { setStatus('JSON解析失败: ' + e.message, 'error'); }
    };
    reader.readAsText(file);
}

async function restoreCatalogFromHandle() {
    try {
        var handle = await dbLoadCatalogHandle();
        if (!handle) return;
        var perm = await handle.queryPermission({ mode: 'read' });
        if (perm !== 'granted') return;
        var file = await handle.getFile();
        processCatalog(file);
        setCookie('hiAutoCatalogFile', file.name, 365);
        return true;
    } catch(e) { return false; }
}

// ==================== 触发器增删 ====================

function addTriggerCond(nodePath, aeTypeName) {
    if (!aeTypeName) return;
    var node = getNodeByPath(nodePath);
    if (!node) return;
    if (!node.TriggerConds) node.TriggerConds = [];
    var info = getAllConditions().find(function(c) { return (c.aeTypeName || c.typeName) === aeTypeName; });
    var newCond = { '$type': aeTypeName };
    getEntryControls(info).forEach(function(ctrl) {
        if (ctrl.defaultValue !== undefined && ctrl.defaultValue !== null) {
            newCond[ctrl.label] = ctrl.defaultValue;
        }
    });
    node.TriggerConds.push(newCond);
    markDirty();
}

function removeTriggerCond(nodePath, idx) {
    var node = getNodeByPath(nodePath);
    if (!node || !node.TriggerConds) return;
    node.TriggerConds.splice(idx, 1);
    markDirty();
}

function addTriggerAction(nodePath, aeTypeName) {
    if (!aeTypeName) return;
    var node = getNodeByPath(nodePath);
    if (!node) return;
    if (!node.TriggerActions) node.TriggerActions = [];
    var info = getAllActions().find(function(a) { return (a.aeTypeName || a.typeName) === aeTypeName; });
    var newAct = { '$type': aeTypeName };
    getEntryControls(info).forEach(function(ctrl) {
        if (ctrl.defaultValue !== undefined && ctrl.defaultValue !== null) {
            newAct[ctrl.label] = ctrl.defaultValue;
        }
    });
    node.TriggerActions.push(newAct);
    markDirty();
}

function removeTriggerAction(nodePath, idx) {
    var node = getNodeByPath(nodePath);
    if (!node || !node.TriggerActions) return;
    node.TriggerActions.splice(idx, 1);
    markDirty();
}

// ==================== UUID ====================

function generateUUID() {
    if (typeof crypto !== 'undefined' && crypto.randomUUID) return crypto.randomUUID();
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
        var r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    });
}

// ==================== 元数据同步 ====================

function syncMeta() {
    if (!timelineData) return;
    document.getElementById('metaAuthor').value = timelineData.Author || '';
    document.getElementById('metaGuid').value = timelineData.Guid || '';
    document.getElementById('metaVarDesc').value = timelineData.ExposedVarDesc || '';
    document.getElementById('metaJob').value = timelineData.TargetJob || 0;
    renderVarList();
}

// ==================== 暴露变量 CRUD ====================

function renderVarList() {
    var list = document.getElementById('varList');
    if (!list || !timelineData) return;
    var vars = timelineData.ExposedVars || timelineData.ExposedVarNames || [];
    var html = '';
    vars.forEach(function(v, i) {
        html += '<div class="var-row" style="display:flex;align-items:center;gap:4px;margin:2px 0">';
        html += '<input value="' + esc(v || '') + '" data-var-idx="' + i + '" style="flex:1;padding:2px 4px;font-size:11px;border:1px solid var(--bd);background:var(--bg1);color:var(--tx1);border-radius:3px">';
        html += '<button class="ed-del-btn" onclick="removeVar(' + i + ')" style="font-size:10px;padding:1px 4px">✕</button>';
        html += '</div>';
    });
    list.innerHTML = html;
    list.querySelectorAll('[data-var-idx]').forEach(function(el) {
        el.addEventListener('change', function() {
            var idx = parseInt(this.dataset.varIdx);
            var vlist = timelineData.ExposedVars || timelineData.ExposedVarNames;
            if (vlist) { vlist[idx] = this.value; markDirty(); }
        });
    });
}

function removeVar(i) {
    var vlist = timelineData.ExposedVars || timelineData.ExposedVarNames;
    if (vlist) { vlist.splice(i, 1); renderVarList(); markDirty(); }
}

// ==================== 工具 ====================

function toggleSection(id) {
    var el = document.getElementById(id);
    var hdr = document.getElementById('hdr' + id.substring(3));
    if (!el) return;
    if (el.style.display === 'none') {
        el.style.display = '';
        if (hdr) hdr.textContent = '▼ ' + hdr.textContent.substring(2);
    } else {
        el.style.display = 'none';
        if (hdr) hdr.textContent = '▶ ' + hdr.textContent.substring(2);
    }
}

// 轴信息面板折叠
var axisPanelOpen = false;
function toggleAxisPanel() {
    var body = document.getElementById('axisPanelBody');
    var bar = document.getElementById('axisPanelBar');
    axisPanelOpen = !axisPanelOpen;
    if (axisPanelOpen) {
        body.classList.remove('hidden');
        if (bar) bar.querySelector('#axisPanelLabel').textContent = '▼ 轴信息';
    } else {
        body.classList.add('hidden');
        if (bar) bar.querySelector('#axisPanelLabel').textContent = '▶ 轴信息';
    }
}

function markDirty() { isDirty = true; renderTree(); renderProps(); updateFooter(); }
function updateFooter() {
    var labels = { execution:'执行轴', assist:'辅助轴' };
    var l = labels[currentAxis]||'';
    document.getElementById('edFooter').textContent = currentFile ? l + ' — ' + currentFile + (isDirty?' (未保存)':'') : '就绪' + (isDirty?' (未保存)':'');
    // 更新信息面板
    var el = document.getElementById('infoType');
    if (el) el.textContent = l;
    el = document.getElementById('infoFile');
    if (el) el.textContent = currentFile || '—';
    el = document.getElementById('infoCount');
    if (el) el.textContent = countNodes();
    el = document.getElementById('infoDirty');
    if (el) { el.textContent = isDirty ? '已修改' : '已保存'; el.style.color = isDirty ? 'var(--orange)' : 'var(--green)'; }
    // 同步副本 ID
    var tidEl = document.getElementById('territoryId');
    if (tidEl && timelineData) tidEl.value = timelineData.TerritoryTypeId || 0;
    // 轴信息摘要（收起时显示）
    var summary = '';
    if (timelineData) {
        var name = timelineData.Name || '';
        var tid = timelineData.TerritoryTypeId || 0;
        var author = timelineData.Author || '';
        summary = (name ? name : '未命名') + (tid ? ' · ' + tid : '') + (author ? ' · ' + author : '');
    }
    var lbl = document.getElementById('axisPanelLabel');
    if (lbl) lbl.textContent = (axisPanelOpen ? '▼' : '▶') + ' ' + (summary || '轴信息');
    syncMeta();
}

function countNodes() {
    if (!timelineData) return 0;
    var c = 0;
    function w(node) { if(!node) return; c++; (node.Childs||node.childs||[]).forEach(w); }
    w(timelineData.TreeRoot||timelineData.treeRoot);
    return c;
}
function setStatus(msg, type) {
    var el = document.getElementById('edStatus'); el.textContent = msg; el.className = 'status '+(type||'');
    if (type==='success') setTimeout(function(){ if(el.textContent===msg){ el.textContent=''; el.className='status'; } }, 3000);
}

function iosSelect(opts, selVal, onchange) {
    var h = '<div class="ios-sel" onclick="event.stopPropagation();this.classList.toggle(\'open\')">';
    opts.forEach(function(opt) {
        if (typeof opt === 'string') opt = {value:opt, label:opt};
        var isSel = opt.value===selVal;
        // 当前选中的选项不阻止冒泡，让父元素处理点击展开/收起
        var clickHandler = isSel ? '' : ' onclick="event.stopPropagation();'+onchange+';this.parentElement.classList.remove(\'open\')"';
        h += '<div class="ios-sel-opt'+(isSel?' sel':'')+'" data-val="'+esc(opt.value)+'"'+clickHandler+'>'+esc(opt.label)+'</div>';
    });
    return h+'</div>';
}
function iosSelectVal(sel, val) { sel.value = val; sel.querySelectorAll('.ios-sel-opt').forEach(function(o){o.classList.toggle('sel',o.dataset.val===val)}); }

function esc(s) { return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }
