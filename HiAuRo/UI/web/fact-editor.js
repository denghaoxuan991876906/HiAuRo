// ============================================================
// HiAuRo 事实轴编辑器 — 数据模型与工具函数
// ============================================================

// ==================== UUID 生成 ====================

function generateUUID() {
    if (typeof crypto !== 'undefined' && crypto.randomUUID) {
        return crypto.randomUUID();
    }
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
        var r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    });
}

// ==================== 常量 ====================

var TIME_STEP = 5;          // 每格秒数
var LINE_HEIGHT = 60;       // 每 TIME_STEP 像素高度
var MIN_TIME = -25;         // 时间轴起始秒数
var MAX_TIME = 300;         // 最大秒数
var MAX_PHASES = 10;        // 阶段数硬上限

var EVENT_COLORS = {
    'None':               '#94a3b8',
    'Ability':            '#00d4ff',
    'StartsUsing':        '#ff9f0a',
    'HeadMarker':         '#ff6b9d',
    'Tether':             '#4ecdc4',
    'AddedCombatant':     '#00f0a0',
    'RemovedCombatant':   '#95e1d3',
    'WasDefeated':        '#ff4477',
    'GainsEffect':        '#f0d000',
    'LosesEffect':        '#f38181',
    'MapEffect':          '#7e57c2',
    'NPCYell':            '#ff9f0a',
    'EnvControl':         '#a78bfa',
    'Weather':            '#38bdf8',
    'ObjectChange':       '#fb923c',
    'ObjectEffect':       '#fbbf24',
    'default':            '#00d4ff'
};

var BRANCH_COLORS = [
    '#7e57c2', '#ff9f0a', '#00f0a0', '#ff4477', '#00d4ff',
    '#f0d000', '#ff6b9d', '#4ecdc4', '#95e1d3', '#f38181'
];

// ==================== 动作模板 ====================

var ACTION_TEMPLATES = {
    demand:           { type: 'demand', '需求减伤': 0, '需求治疗': 0 },
    skillSuggestion:  { type: 'skillSuggestion', skillId: 0, label: '', priority: 'normal' },
    setVariable:      { type: 'setVariable', variableName: '', value: true },
    toggleVariable:   { type: 'toggleVariable', variableName: '' },
    logMessage:       { type: 'logMessage', message: '' },
    switchPhase:      { type: 'switchPhase', targetPhase: '', label: '' },
    switchBranch:     { type: 'switchBranch', condition: '', targetBranch: '' }
};

// ==================== 状态变量 ====================

var timelineData = null;       // 当前事实轴数据 { name, territoryId, author, phases:[...] }
var currentFile = '';          // 当前文件名
var fileHandle = null;         // FileSystemFileHandle
var isDirty = false;           // 未保存修改标记
var selectedEventPath = null;  // 选中事件路径 (如 'p0_ev0')
var currentPhaseIdx = 0;       // 当前编辑的阶段索引
var collapsedPhases = {};      // 左侧面板阶段折叠状态
var collapsedBranches = {};    // 左侧面板分支折叠状态
// ---- 录制数据 ----
var recordingData = null;
var recordingChecked = {};
var recordingFilters = {
    cast: true, ability: true, buff: true, tether: true,
    spawn: true, death: true, target: true, combat: true,
    environment: true, npc: true, director: true,
    actorControl: false, chat: false
};

function setStatus(msg, type) {
    var el = document.getElementById('footer');
    if (!el) return;
    el.textContent = msg;
    if (type === 'error') {
        el.style.color = 'var(--red)';
        setTimeout(function() { el.style.color = ''; updateFooter(); }, 3000);
    } else if (type === 'success') {
        el.style.color = '#30d158';
        setTimeout(function() { el.style.color = ''; updateFooter(); }, 2000);
    }
}

// ==================== 右键菜单状态 ====================

var ctxMenuClickTime = 0;     // 画布右键点击计算的时间位置
var ctxMenuTargetPath = '';   // 当前右键目标事件路径
var ctxMenuBranchIdx = -1;    // 右键目标分支索引 (-1=主阶段)
var frozenMarkerTime = null;  // 冻结的横线时间（null=正常追踪）
var dragState = { active: false, srcPath: null, startY: 0, moved: false, preventClick: false };
var _dragHandlersBound = false; // 防止重复绑定拖拽事件

// ==================== 工具函数 ====================

function toggleSection(id) {
    var el = document.getElementById(id);
    var hdrId = 'hdr' + id.substring(3);
    var hdr = document.getElementById(hdrId);
    if (!el) return;
    if (el.style.display === 'none') {
        el.style.display = '';
        if (hdr) hdr.textContent = '▼ ' + hdr.textContent.substring(2);
    } else {
        el.style.display = 'none';
        if (hdr) hdr.textContent = '▶ ' + hdr.textContent.substring(2);
    }
}

function esc(s) {
    return String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function formatTime(t) {
    t = Number(t) || 0;
    var neg = t < 0;
    if (neg) t = -t;
    var s;
    if (t < 60) s = t.toFixed(1) + 's';
    else { var m = Math.floor(t / 60); var sec = Math.round(t % 60); s = m + ':' + (sec < 10 ? '0' : '') + sec; }
    return (neg ? '-' : '') + s;
}

/** 时间 → 像素 (相对于容器顶部) */
function timeToY(time) {
    return (time - MIN_TIME) / TIME_STEP * LINE_HEIGHT;
}

/** 设置画布高度 */
function setCanvasHeight() {
    var h = timeToY(MAX_TIME) + 'px';
    var body = document.querySelector('.timeline-body');
    if (body) { body.style.minHeight = h; body.style.height = h; }
    var ts = document.getElementById('timeScale');
    if (ts) { ts.style.minHeight = h; ts.style.height = h; }
}

function newTimeline() {
    return {
        name: '新事实轴',
        territoryId: 0,
        author: '',
        guid: generateUUID(),
        note: '',
        phases: [
            { id: 'p1', name: 'P1', events: [], switch: null }
        ]
    };
}

function getPhase(phaseIdx) {
    if (!timelineData || !timelineData.phases) return null;
    return timelineData.phases[phaseIdx] || null;
}

function getEventByPath(path) {
    if (!path || !timelineData) return null;
    var parts = path.split('_');
    if (parts.length < 2) return null;
    var phaseIdx = parseInt(parts[0].substring(1));
    if (isNaN(phaseIdx)) return null;
    var phase = getPhase(phaseIdx);
    if (!phase) return null;

    if (parts[1] === 'switch' && parts.length === 4) {
        // p{idx}_switch_br{idx}_ev{idx}
        var brIdx = parseInt(parts[2].substring(2));
        var evIdx = parseInt(parts[3].substring(2));
        if (isNaN(brIdx) || isNaN(evIdx)) return null;
        if (!phase.switch || !phase.switch.branches) return null;
        var branch = phase.switch.branches[brIdx];
        if (!branch || !branch.events) return null;
        return branch.events[evIdx] || null;
    } else if (parts.length === 2) {
        // p{idx}_ev{idx}
        var evIdx = parseInt(parts[1].substring(2));
        if (isNaN(evIdx)) return null;
        return phase.events[evIdx] || null;
    }
    return null;
}

function getParentInfo(path) {
    if (!path || !timelineData) return null;
    var parts = path.split('_');
    if (parts.length < 2) return null;
    var phaseIdx = parseInt(parts[0].substring(1));
    if (isNaN(phaseIdx)) return null;
    var phase = getPhase(phaseIdx);
    if (!phase) return null;

    if (parts[1] === 'switch' && parts.length === 4) {
        // p{idx}_switch_br{idx}_ev{idx}
        var brIdx = parseInt(parts[2].substring(2));
        var evIdx = parseInt(parts[3].substring(2));
        if (isNaN(brIdx) || isNaN(evIdx)) return null;
        if (!phase.switch || !phase.switch.branches) return null;
        var branch = phase.switch.branches[brIdx];
        if (!branch || !branch.events) return null;
        return { container: branch.events, idx: evIdx, isSubBranch: true };
    } else if (parts.length === 2) {
        // p{idx}_ev{idx}
        var evIdx = parseInt(parts[1].substring(2));
        if (isNaN(evIdx)) return null;
        return { container: phase.events, idx: evIdx, isSubBranch: false };
    }
    return null;
}

function getEventColor(ev) {
    if (!ev) return EVENT_COLORS.default;
    var typeName = ev.type || ev.Type || 'None';
    return EVENT_COLORS[typeName] || EVENT_COLORS.default;
}

function markDirty() {
    if (markDirty._timer) clearTimeout(markDirty._timer);
    markDirty._timer = setTimeout(function() {
        isDirty = true;
        renderAll();
        updateFooter();
    }, 30);
}

// ==================== 渲染 (占位 — T5~T8 实现) ====================

function renderAll() { setCanvasHeight(); renderPhaseList(); renderPhaseTabs(); renderTimeScale(); renderPhaseTracks(); renderEvents(); renderProps(); bindDragHandlers(); }

// ==================== 阶段管理 ====================

/** 切换到指定阶段 */
function switchPhase(idx) {
    currentPhaseIdx = idx;
    renderAll();
}

/** 渲染左侧阶段列表（树形视图） */
function renderPhaseList() {
    var el = document.getElementById('phaseList');
    if (!el) return;

    if (!timelineData || !timelineData.phases) {
        el.innerHTML = '<div class="hint">暂无阶段</div>';
        return;
    }

    var phases = timelineData.phases;
    var html = '';
    for (var i = 0; i < phases.length; i++) {
        var ph = phases[i];
        var activeClass = (i === currentPhaseIdx) ? ' active' : '';
        var collapsed = collapsedPhases[i];
        var toggleClass = collapsed ? '' : ' expanded';
        var childClass = collapsed ? ' collapsed' : '';

        html += '<div class="phase-item' + activeClass + '" data-idx="' + i + '">';
        html += '<span class="phase-toggle' + toggleClass + '" data-idx="' + i + '" title="展开/折叠">▶</span>';
        html += '<span class="phase-name" data-idx="' + i + '">' + esc(ph.name) + '</span>';
        html += '<span style="font-size:10px;color:var(--tx2);margin-right:4px">' + (ph.events ? ph.events.length : 0) + '事件</span>';
        if (ph.switch && ph.switch.branches && ph.switch.branches.length) {
            html += '<span style="font-size:10px;color:var(--tx2);margin-right:4px">' + ph.switch.branches.length + '分支</span>';
        }
        if (phases.length > 1) {
            html += '<button class="phase-del" data-idx="' + i + '" title="删除阶段">×</button>';
        }
        html += '</div>';

        // 展开的子项
        html += '<div class="phase-children' + childClass + '" data-phase-idx="' + i + '">';
        // 事件 (按时间排序)
        if (ph.events && ph.events.length) {
            var sortedEvents = ph.events.slice().sort(function(a, b) { return a.time - b.time; });
            for (var se = 0; se < sortedEvents.length; se++) {
                var ev = sortedEvents[se];
                var origIdx = ph.events.indexOf(ev);
                var evPath = 'p' + i + '_ev' + origIdx;
                var evColor = getEventColor(ev);
                var evActive = selectedEventPath === evPath ? ' active' : '';
                html += '<div class="tree-event' + evActive + '" data-path="' + evPath + '" style="padding-left:24px">';
                html += '<span class="tree-dot" style="background:' + evColor + '"></span>';
                html += '<span>' + esc(formatTime(ev.time)) + ' | ' + esc(ev.name) + '</span>';
                html += '</div>';

                // 如果此事件触发了分支切换（是switch event），且当前phase有switch分支
                var isSwitchEv = ev.actions && ev.actions.length && ev.actions[0].type === 'switchBranch';
                if (isSwitchEv && ph.switch && ph.switch.branches && ph.switch.branches.length) {
                    for (var brIdx = 0; brIdx < ph.switch.branches.length; brIdx++) {
                        var br = ph.switch.branches[brIdx];
                        var brKey = 'p' + i + '_br' + brIdx;
                        var brCollapsed = collapsedBranches[brKey];
                        var brToggle = brCollapsed ? '▶' : '▼';
                        html += '<div class="tree-branch-header" data-branch-key="' + brKey + '" style="padding-left:40px">';
                        html += '<span class="tree-branch-toggle">' + brToggle + '</span>';
                        html += '<span>分支' + (brIdx + 1) + '</span>';
                        html += '</div>';
                        if (!brCollapsed && br.events && br.events.length) {
                            var sortedBrEvents = br.events.slice().sort(function(a, b) { return a.time - b.time; });
                            for (var sbe = 0; sbe < sortedBrEvents.length; sbe++) {
                                var brEv = sortedBrEvents[sbe];
                                var brOrigIdx = br.events.indexOf(brEv);
                                var brEvPath = 'p' + i + '_switch_br' + brIdx + '_ev' + brOrigIdx;
                                var brEvActive = selectedEventPath === brEvPath ? ' active' : '';
                                html += '<div class="tree-event' + brEvActive + '" data-path="' + brEvPath + '" style="padding-left:56px">';
                                html += '<span class="tree-dot" style="background:' + BRANCH_COLORS[brIdx % BRANCH_COLORS.length] + '"></span>';
                                html += '<span>' + esc(formatTime(brEv.time)) + ' | ' + esc(brEv.name) + '</span>';
                                html += '</div>';
                            }
                        }
                    }
                }
            }
        }
        html += '</div>';
    }

    var canAdd = phases.length < MAX_PHASES;
    html += '<button class="phase-add" id="btnAddPhase"' + (canAdd ? '' : ' disabled') + '>' + esc(canAdd ? '+ 添加阶段' : '已达上限 (' + MAX_PHASES + ')') + '</button>';

    el.innerHTML = html;

    // 阶段点击切换
    el.querySelectorAll('.phase-item').forEach(function(item) {
        item.addEventListener('click', function(e) {
            if (e.target.closest('.phase-del') || e.target.closest('.phase-toggle')) return;
            var idx = parseInt(this.dataset.idx);
            if (!isNaN(idx) && idx !== currentPhaseIdx) {
                switchPhase(idx);
            }
        });
    });

    // 折叠/展开阶段
    el.querySelectorAll('.phase-toggle').forEach(function(toggle) {
        toggle.addEventListener('click', function(e) {
            e.stopPropagation();
            var idx = parseInt(this.dataset.idx);
            if (isNaN(idx)) return;
            collapsedPhases[idx] = !collapsedPhases[idx];
            renderPhaseList(); // 重新渲染
        });
    });

    // 折叠/展开分支
    el.querySelectorAll('.tree-branch-header').forEach(function(header) {
        header.addEventListener('click', function(e) {
            e.stopPropagation();
            var key = this.dataset.branchKey;
            if (!key) return;
            collapsedBranches[key] = !collapsedBranches[key];
            renderPhaseList();
        });
    });

    // 事件点击（树中选中）
    el.querySelectorAll('.tree-event').forEach(function(evEl) {
        evEl.addEventListener('click', function(e) {
            e.stopPropagation();
            var path = this.dataset.path;
            if (!path) return;
            selectedEventPath = path;
            // 同步更新时间轴
            var phaseMatch = path.match(/^p(\d+)/);
            if (phaseMatch) {
                var pIdx = parseInt(phaseMatch[1]);
                if (!isNaN(pIdx) && pIdx !== currentPhaseIdx) {
                    currentPhaseIdx = pIdx;
                }
            }
            markDirty();
        });
    });

    // 双击重命名
    el.querySelectorAll('.phase-name').forEach(function(span) {
        span.addEventListener('dblclick', function(e) {
            e.stopPropagation();
            var idx = parseInt(this.dataset.idx);
            if (!isNaN(idx)) startRename(idx, this);
        });
    });

    // 删除按钮（三击确认，1s内连续点击3次）
    el.querySelectorAll('.phase-del').forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.stopPropagation();
            var idx = parseInt(this.dataset.idx);
            if (isNaN(idx)) return;
            var now = Date.now();
            var key = 'p' + idx;
            var last = phaseDeleteClicks[key] || { count: 0, time: 0 };
            if (now - last.time < 1000) {
                last.count++;
                last.time = now;
            } else {
                last = { count: 1, time: now };
            }
            phaseDeleteClicks[key] = last;
            if (last.count >= 3) {
                delete phaseDeleteClicks[key];
                deletePhase(idx);
            }
        });
    });

    // 添加阶段按钮
    var addBtn = document.getElementById('btnAddPhase');
    if (addBtn) addBtn.addEventListener('click', addPhase);
}

/** 渲染工具栏阶段选项卡 */
function renderPhaseTabs() {
    var el = document.getElementById('phaseTabs');
    if (!el) return;

    if (!timelineData || !timelineData.phases || timelineData.phases.length === 0) {
        el.innerHTML = '';
        return;
    }

    var html = '';
    for (var i = 0; i < timelineData.phases.length; i++) {
        var active = (i === currentPhaseIdx) ? ' active' : '';
        html += '<button class="tab' + active + '" data-idx="' + i + '" onclick="switchPhase(' + i + ')">' + esc(timelineData.phases[i].name) + '</button>';
    }
    el.innerHTML = html;

    // 多阶段时允许横向滚动
    el.style.overflowX = 'auto';
    el.style.flexWrap = 'nowrap';
}

/** 双击行内重命名 */
function startRename(idx, span) {
    var phase = getPhase(idx);
    if (!phase) return;

    var input = document.createElement('input');
    input.type = 'text';
    input.className = 'phase-rename-input';
    input.value = phase.name;

    span.style.display = 'none';
    span.parentNode.insertBefore(input, span.nextSibling);
    input.focus();
    input.select();

    function finish() {
        var val = input.value.trim();
        if (val && val !== phase.name) {
            phase.name = val;
            markDirty();
            return; // markDirty 已通过 renderAll 重绘了列表
        }
        // 取消或无变化 — 恢复显示
        input.remove();
        span.style.display = '';
    }

    input.addEventListener('blur', finish);
    input.addEventListener('keydown', function(e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            input.blur();
        } else if (e.key === 'Escape') {
            input.value = phase.name; // 重置
            input.blur();
        }
    });
}

/** 查找最小可用阶段编号 */
function findLowestPhaseNum() {
    var used = {};
    for (var i = 0; i < timelineData.phases.length; i++) {
        var m = (timelineData.phases[i].id || '').match(/^p(\d+)$/);
        if (m) used[parseInt(m[1])] = true;
    }
    for (var n = 1; n <= MAX_PHASES; n++) {
        if (!used[n]) return n;
    }
    return timelineData.phases.length + 1;
}

/** 添加新阶段 */
function addPhase() {
    if (!timelineData) return;
    if (timelineData.phases.length >= MAX_PHASES) return;

    var newNum = findLowestPhaseNum();
    timelineData.phases.push({
        id: 'p' + newNum,
        name: 'P' + newNum,
        events: [],
        switch: null
    });
    currentPhaseIdx = timelineData.phases.length - 1;
    selectedEventPath = null;
    markDirty();
}

/** 删除阶段 */
function deletePhase(idx) {
    if (!timelineData || timelineData.phases.length <= 1) return;

    timelineData.phases.splice(idx, 1);

    // 调整 currentPhaseIdx
    if (idx < currentPhaseIdx) {
        currentPhaseIdx--;
    }
    if (currentPhaseIdx >= timelineData.phases.length) {
        currentPhaseIdx = timelineData.phases.length - 1;
    }

    selectedEventPath = null;
    markDirty();
}

function syncFactMeta() {
    if (!timelineData) return;
    var el;
    el = document.getElementById('factName'); if (el) el.value = timelineData.name || '';
    el = document.getElementById('factTerritoryId'); if (el) el.value = timelineData.territoryId || 0;
    el = document.getElementById('factAuthor'); if (el) el.value = timelineData.author || '';
    el = document.getElementById('factGuid'); if (el) el.value = timelineData.guid || '';
    el = document.getElementById('factNote'); if (el) el.value = timelineData.note || '';
}

function renderTimeScale() {
    var el = document.getElementById('timeScale');
    if (!el) return;
    el.innerHTML = '';
    var firstTick = Math.ceil(MIN_TIME / TIME_STEP) * TIME_STEP;
    for (var t = firstTick; t <= MAX_TIME; t += TIME_STEP) {
        var div = document.createElement('div');
        div.className = 'time-line';
        if (t === 0) div.classList.add('time-line-zero');
        div.style.position = 'absolute';
        div.style.left = '0';
        div.style.right = '0';
        div.style.top = timeToY(t) + 'px';
        div.innerHTML = '<span>' + esc(formatTime(t)) + '</span>';
        el.appendChild(div);
    }
}

function renderPhaseTracks() {
    var el = document.getElementById('phaseTracks');
    if (!el) return;

    if (!timelineData || !timelineData.phases || timelineData.phases.length === 0) {
        el.innerHTML = '<div class="hint">暂无阶段数据，点击"新建"开始创建</div>';
        return;
    }

    el.innerHTML = '';
    // 横向 flex 容器
    el.style.display = 'flex';
    el.style.gap = '24px';

    for (var i = 0; i < timelineData.phases.length; i++) {
        var phase = timelineData.phases[i];
        var track = document.createElement('div');
        track.className = 'phase-track';
        track.setAttribute('data-phase-idx', i);

        var label = document.createElement('div');
        label.className = 'track-label';
        label.textContent = phase.name; // textContent 自带转义

        var glare = document.createElement('div');
        glare.className = 'track-glare';

        var mainLine = document.createElement('div');
        mainLine.className = 'track-main-line';
        if (phase.events && phase.events.length > 0) {
            mainLine.style.background = getEventColor(phase.events[0]);
        } else {
            mainLine.style.background = 'var(--accent)';
        }

        // 检查是否有 switchPhase 事件，如果有则截断主线到此事件时间
        var switchPhaseTime = null;
        if (phase.events && phase.events.length) {
            for (var ei = 0; ei < phase.events.length; ei++) {
                var evt = phase.events[ei];
                if (evt.actions) {
                    for (var ai = 0; ai < evt.actions.length; ai++) {
                        if (evt.actions[ai].type === 'switchPhase') {
                            switchPhaseTime = evt.time;
                            break;
                        }
                    }
                }
                if (switchPhaseTime !== null) break;
            }
        }
        if (switchPhaseTime !== null) {
            mainLine.style.flex = 'none';
            mainLine.style.minHeight = '0px';
            mainLine.style.height = timeToY(switchPhaseTime) + 'px';
        }

        track.appendChild(label);
        track.appendChild(glare);
        track.appendChild(mainLine);
        el.appendChild(track);
    }
}

function renderEvents() {
    var allTracks = document.querySelectorAll('.phase-track');
    // 清除所有阶段的旧事件
    for (var ti = 0; ti < allTracks.length; ti++) {
        var oldNodes = allTracks[ti].querySelectorAll('.event-node, .event-label, .event-label-alt');
        for (var n = 0; n < oldNodes.length; n++) oldNodes[n].remove();
    }

    if (!timelineData || !timelineData.phases) { updateFooter(); return; }

    for (var pIdx = 0; pIdx < timelineData.phases.length; pIdx++) {
        var track = document.querySelector('.phase-track[data-phase-idx="' + pIdx + '"]');
        if (!track) continue;
        var phase = timelineData.phases[pIdx];
        if (!phase || !phase.events || phase.events.length === 0) continue;

        var events = phase.events;
        var lastRightTop = -9999;
        var lastLeftTop = -9999;

        for (var i = 0; i < events.length; i++) {
            var ev = events[i];
            var top = timeToY(ev.time);

        // ---- 事件节点 ----
        var node = document.createElement('div');
        node.className = 'event-node';
        node.style.top = top + 'px';
        node.dataset.path = 'p' + pIdx + '_ev' + i;
        node.title = esc(ev.name) + ' (' + formatTime(ev.time) + ')';

        var color = getEventColor(ev);
        node.style.background = color;
        node.style.boxShadow = '0 0 8px ' + color;

        // 已选中的恢复高亮
        if (selectedEventPath === node.dataset.path) {
            node.classList.add('sel');
        }

        // 点击选中
        (function(path, el) {
            el.addEventListener('click', function(e) {
                if (dragState.preventClick) return;
                e.stopPropagation();
                selectedEventPath = path;

                // 清除所有节点的 .sel
                var allNodes = track.querySelectorAll('.event-node');
                for (var a = 0; a < allNodes.length; a++) {
                    allNodes[a].classList.remove('sel');
                }
                el.classList.add('sel');

                renderProps();
            });
        })(node.dataset.path, node);

        // ---- 事件标签 ----
        var label = document.createElement('span');
        label.style.top = top + 'px';
        label.textContent = formatTime(ev.time) + ' | ' + ev.name;

        // 防重叠：与同一侧标签垂直距离 < 40px 则换侧；换侧后仍重叠则隐藏
        var distRight = Math.abs(top - lastRightTop);
        var distLeft = Math.abs(top - lastLeftTop);
        var useLeft = false;
        var hideLabel = false;

        if (distRight < 40) {
            useLeft = true;
            if (distLeft < 40) {
                hideLabel = true;
            }
        }

        if (hideLabel) {
            label.style.display = 'none';
        } else if (useLeft) {
            label.className = 'event-label-alt';
            lastLeftTop = top;
        } else {
            label.className = 'event-label';
            lastRightTop = top;
        }

        track.appendChild(node);
        track.appendChild(label);
        } // end inner event loop
    } // end outer phase loop

    // 渲染子分支
    renderAllSubBranches();

    updateFooter();
}

// 渲染当前阶段的所有子分支
function renderAllSubBranches() {
    // 清除所有阶段的旧子分支
    var allBranches = document.querySelectorAll('.sub-branch');
    for (var ab = 0; ab < allBranches.length; ab++) allBranches[ab].remove();

    if (!timelineData || !timelineData.phases) return;

    for (var curIdx = 0; curIdx < timelineData.phases.length; curIdx++) {
        var track = document.querySelector('.phase-track[data-phase-idx="' + curIdx + '"]');
        if (!track) continue;
        var phase = timelineData.phases[curIdx];
        if (!phase || !phase.switch || !phase.switch.branches || phase.switch.branches.length === 0) continue;

    // 找到 switch 事件（action type 为 switchBranch），否则用最后一个事件
    var events = phase.events || [];
    var switchEvent = null;
    for (var i = 0; i < events.length; i++) {
        if (events[i].actions && events[i].actions.length > 0 && events[i].actions[0].type === 'switchBranch') {
            switchEvent = events[i];
            break;
        }
    }
    if (!switchEvent && events.length > 0) switchEvent = events[events.length - 1];
    if (!events.length) return;

    var branchOriginTop = switchEvent ? timeToY(switchEvent.time) : timeToY(0);
    // 主轴在 .phase-track 中居中 (100px 宽), 主轴线约在 45px 处
    var mainLineX = 45;
    var branches = phase.switch.branches;

    for (var brIdx = 0; brIdx < branches.length; brIdx++) {
        var branch = branches[brIdx];
        var brColor = BRANCH_COLORS[brIdx % BRANCH_COLORS.length];
        var branchX = mainLineX + 28 + brIdx * 28; // 分支线在主轴线右侧

        // ---- 分支容器 ----
        var container = document.createElement('div');
        container.className = 'sub-branch';
        container.setAttribute('data-branch-idx', brIdx);
        container.setAttribute('data-phase-idx', curIdx);
        container.style.top = branchOriginTop + 'px';
        container.style.left = branchX + 'px';
        // 容器宽度 = 其子元素最大宽度
        container.style.minWidth = '40px';

        // ---- 横向折线 (从主轴线连到分支线) ----
        var hook = document.createElement('div');
        hook.className = 'sub-branch-hook';
        hook.style.width = (branchX - mainLineX) + 'px';
        hook.style.position = 'absolute';
        hook.style.left = '-' + (branchX - mainLineX) + 'px';
        hook.style.top = '-1px';
        hook.style.background = brColor;
        hook.style.boxShadow = '0 0 4px ' + brColor;
        container.appendChild(hook);

        // ---- 分支轨道线 (从切换点向下延伸至时间轴底部) ----
        var trackLine = document.createElement('div');
        trackLine.className = 'sub-branch-track';
        trackLine.style.background = brColor;
        trackLine.style.boxShadow = '0 0 4px ' + brColor;
        // 延伸至 MAX_TIME（从切换事件时间到最大时间），若有 switchPhase 则截断
        var branchEndTime = MAX_TIME;
        if (branch.events) {
            for (var bei = 0; bei < branch.events.length; bei++) {
                var bev = branch.events[bei];
                if (bev.actions) {
                    for (var bai = 0; bai < bev.actions.length; bai++) {
                        if (bev.actions[bai].type === 'switchPhase') {
                            branchEndTime = bev.time; // bev.time 已是绝对时间
                            break;
                        }
                    }
                }
                if (branchEndTime < MAX_TIME) break;
            }
        }
        var remainingTime = branchEndTime - (switchEvent ? switchEvent.time : 0);
        trackLine.style.flex = 'none';
        trackLine.style.minHeight = '0px';
        trackLine.style.height = (remainingTime / TIME_STEP * LINE_HEIGHT) + 'px';
        container.appendChild(trackLine);

        // ---- 分支事件 ----
        for (var evIdx = 0; evIdx < branch.events.length; evIdx++) {
            var ev = branch.events[evIdx];
            // 绝对时间 → 容器内像素偏移
            var evTop = (ev.time - (switchEvent ? switchEvent.time : 0)) / TIME_STEP * LINE_HEIGHT;
            var path = 'p' + curIdx + '_switch_br' + brIdx + '_ev' + evIdx;

            var node = document.createElement('div');
            node.className = 'sub-branch-event';
            node.style.top = evTop + 'px';
            node.style.background = brColor;
            node.style.boxShadow = '0 0 6px ' + brColor;
            node.dataset.path = path;
            node.title = esc(ev.name) + ' (' + formatTime(ev.time) + ')';

            if (selectedEventPath === path) node.classList.add('sel');

            (function(p, el) {
                el.addEventListener('click', function(e) {
                    e.stopPropagation();
                    if (dragState.preventClick) return;
                    selectedEventPath = p;
                    var allNodes = track.querySelectorAll('.event-node, .sub-branch-event');
                    for (var a = 0; a < allNodes.length; a++) allNodes[a].classList.remove('sel');
                    el.classList.add('sel');
                    renderProps();
                });
            })(path, node);

            var evLabel = document.createElement('span');
            evLabel.className = 'event-label';
            evLabel.style.top = evTop + 'px';
            evLabel.textContent = formatTime(ev.time) + ' | ' + ev.name;

            container.appendChild(node);
            container.appendChild(evLabel);
        }

        track.appendChild(container);
        } // end branch loop
    } // end phase loop
}

function renderProps() {
    var panel = document.getElementById('propPanel');
    if (!panel) return;
    panel.innerHTML = '';

    if (!selectedEventPath) {
        panel.innerHTML = '<div class="hint">点击事件查看属性</div>';
        return;
    }

    var ev = getEventByPath(selectedEventPath);
    if (!ev) {
        panel.innerHTML = '<div class="hint">事件未找到</div>';
        return;
    }

    var html = '';

    // ==================== 基本信息 ====================
    html += '<div class="prop-section">';
    html += '<div class="prop-section-header">基本信息</div>';

    // 名称
    html += '<div class="prop-row">';
    html += '<span class="prop-label">名称</span>';
    html += '<input class="prop-input" type="text" value="' + esc(ev.name || '') + '" onchange="updateEventProp(\'name\', this.value)">';
    html += '</div>';

    // ID (只读)
    html += '<div class="prop-row">';
    html += '<span class="prop-label">ID</span>';
    html += '<input class="prop-input" type="text" value="' + esc(ev.id || '') + '" readonly>';
    html += '</div>';

    // 时间(s)
    html += '<div class="prop-row">';
    html += '<span class="prop-label">时间(s)</span>';
    html += '<input class="prop-input" type="number" value="' + (ev.time || 0) + '" step="0.1" onchange="updateEventNumProp(\'time\', this.value)">';
    html += '</div>';

    // 持续(s)
    html += '<div class="prop-row">';
    html += '<span class="prop-label">持续(s)</span>';
    html += '<input class="prop-input" type="number" value="' + (ev.duration || 0) + '" step="0.1" onchange="updateEventNumProp(\'duration\', this.value)">';
    html += '</div>';

    // 事件类型
    html += '<div class="prop-row">';
    html += '<span class="prop-label">事件类型</span>';
    html += '<select class="prop-input" onchange="updateEventProp(\'type\', this.value)">';
    var types = ['None','Ability','StartsUsing','HeadMarker','Tether','AddedCombatant','RemovedCombatant','WasDefeated','GainsEffect','LosesEffect','MapEffect','NPCYell','EnvControl','Weather','ObjectChange','ObjectEffect'];
    var currentType = ev.type || ev.Type || 'None';
    for (var i = 0; i < types.length; i++) {
        html += '<option value="' + types[i] + '"' + (currentType === types[i] ? ' selected' : '') + '>' + types[i] + '</option>';
    }
    html += '</select></div>';

    // 能力ID
    html += '<div class="prop-row">';
    html += '<span class="prop-label">能力ID</span>';
    html += '<input class="prop-input" type="number" value="' + (ev.abilityId || 0) + '" onchange="updateEventNumProp(\'abilityId\', this.value)">';
    html += '</div>';

    // 目标可选中
    html += '<div class="prop-row">';
    html += '<span class="prop-label">可选中</span>';
    html += '<select class="prop-input" onchange="updateTargetable(this.value)">';
    var tgtVal = ev.targetable;
    html += '<option value=""' + (tgtVal === undefined || tgtVal === null ? ' selected' : '') + '>-</option>';
    html += '<option value="true"' + (tgtVal === true ? ' selected' : '') + '>可选中</option>';
    html += '<option value="false"' + (tgtVal === false ? ' selected' : '') + '>不可选中</option>';
    html += '</select></div>';

    html += '</div>'; // end 基本信息

    // ==================== 同步校准 ====================
    html += '<div class="prop-section">';
    html += '<div class="prop-section-header">同步校准</div>';

    function renderSyncSection(side) {
        if (ev[side]) {
            html += '<div class="prop-row">';
            html += '<span class="prop-label">前窗口(s)</span>';
            html += '<input class="prop-input" type="number" value="' + (ev[side].windowBefore != null ? ev[side].windowBefore : 2.5) + '" step="0.5" onchange="updateSyncNumProp(\'' + side + '\',\'windowBefore\',this.value)">';
            html += '</div>';
            html += '<div class="prop-row">';
            html += '<span class="prop-label">后窗口(s)</span>';
            html += '<input class="prop-input" type="number" value="' + (ev[side].windowAfter != null ? ev[side].windowAfter : 2.5) + '" step="0.5" onchange="updateSyncNumProp(\'' + side + '\',\'windowAfter\',this.value)">';
            html += '</div>';
            html += '<div class="prop-row">';
            html += '<span class="prop-label">跳转(s)</span>';
            html += '<input class="prop-input" type="number" value="' + (ev[side].jump != null ? ev[side].jump : '') + '" step="0.1" placeholder="不跳转" onchange="updateSyncNumProp(\'' + side + '\',\'jump\',this.value)">';
            html += '</div>';
            if (side === 'startSync') {
                html += '<div class="prop-row">';
                html += '<span class="prop-label">强制跳转</span>';
                html += '<input type="checkbox" style="width:auto;"' + (ev[side].forcejump ? ' checked' : '') + ' onchange="updateSyncBoolProp(\'' + side + '\',\'forcejump\',this.checked)">';
                html += '</div>';
                if (ev[side].forcejump) {
                    html += '<div class="prop-row">';
                    html += '<span class="prop-label">强跳目标(s)</span>';
                    html += '<input class="prop-input" type="number" value="' + (ev[side].forcejumpTarget != null ? ev[side].forcejumpTarget : '') + '" step="0.1" onchange="updateSyncNumProp(\'' + side + '\',\'forcejumpTarget\',this.value)">';
                    html += '</div>';
                }
            }
            html += '<button class="btn danger" style="margin-bottom:8px;" onclick="removeSync(\'' + side + '\')">移除</button>';
        } else {
            html += '<button class="btn" style="margin-bottom:4px;" onclick="addSync(\'' + side + '\')">+ 添加</button>';
        }
    }

    renderSyncSection('startSync');
    renderSyncSection('endSync');

    html += '</div>'; // end 同步校准

    // ==================== 动作列表 ====================
    html += '<div class="prop-section">';
    html += '<div class="prop-section-header">动作</div>';

    var actions = ev.actions || [];
    for (var i = 0; i < actions.length; i++) {
        var a = actions[i];
        html += '<div class="action-item">';

        // 动作类型下拉
        html += '<div class="prop-row">';
        html += '<span class="prop-label">类型</span>';
        html += '<select class="prop-input" onchange="updateActionType(' + i + ', this.value)">';
        html += '<option value="demand"' + (a.type === 'demand' ? ' selected' : '') + '>需求</option>';
        html += '<option value="skillSuggestion"' + (a.type === 'skillSuggestion' ? ' selected' : '') + '>技能建议</option>';
        html += '<option value="setVariable"' + (a.type === 'setVariable' ? ' selected' : '') + '>设置变量</option>';
        html += '<option value="toggleVariable"' + (a.type === 'toggleVariable' ? ' selected' : '') + '>切换变量</option>';
        html += '<option value="logMessage"' + (a.type === 'logMessage' ? ' selected' : '') + '>日志</option>';
        html += '<option value="switchPhase"' + (a.type === 'switchPhase' ? ' selected' : '') + '>切换阶段</option>';
        html += '<option value="switchBranch"' + (a.type === 'switchBranch' ? ' selected' : '') + '>切换分支</option>';
        html += '</select>';
        html += '</div>';

        // 按类型渲染字段
        if (a.type === 'demand') {
            html += '<div class="prop-row"><span class="prop-label">减伤%</span><input class="prop-input" type="number" value="' + (a['需求减伤'] || 0) + '" onchange="updateActionProp(' + i + ', \'需求减伤\', parseFloat(this.value) || 0)"></div>';
            html += '<div class="prop-row"><span class="prop-label">治疗</span><input class="prop-input" type="number" value="' + (a['需求治疗'] || 0) + '" onchange="updateActionProp(' + i + ', \'需求治疗\', parseFloat(this.value) || 0)"></div>';
        } else if (a.type === 'skillSuggestion') {
            html += '<div class="prop-row"><span class="prop-label">技能ID</span><input class="prop-input" type="number" value="' + (a.skillId || 0) + '" onchange="updateActionProp(' + i + ', \'skillId\', parseInt(this.value) || 0)"></div>';
            html += '<div class="prop-row"><span class="prop-label">名称</span><input class="prop-input" type="text" value="' + esc(a.label || '') + '" onchange="updateActionProp(' + i + ', \'label\', this.value)"></div>';
            html += '<div class="prop-row"><span class="prop-label">优先级</span>';
            html += '<select class="prop-input" onchange="updateActionProp(' + i + ', \'priority\', this.value)">';
            html += '<option value="high"' + (a.priority === 'high' ? ' selected' : '') + '>high</option>';
            html += '<option value="normal"' + (a.priority === 'normal' ? ' selected' : '') + '>normal</option>';
            html += '<option value="optional"' + (a.priority === 'optional' ? ' selected' : '') + '>optional</option>';
            html += '</select></div>';
        } else if (a.type === 'setVariable') {
            html += '<div class="prop-row"><span class="prop-label">变量名</span><input class="prop-input" type="text" value="' + esc(a.variableName || '') + '" onchange="updateActionProp(' + i + ', \'variableName\', this.value)"></div>';
            html += '<div class="prop-row"><span class="prop-label">值</span>';
            html += '<select class="prop-input" onchange="updateActionProp(' + i + ', \'value\', this.value === \'true\')">';
            html += '<option value="true"' + (a.value === true ? ' selected' : '') + '>true</option>';
            html += '<option value="false"' + (a.value === false ? ' selected' : '') + '>false</option>';
            html += '</select></div>';
        } else if (a.type === 'toggleVariable') {
            html += '<div class="prop-row"><span class="prop-label">变量名</span><input class="prop-input" type="text" value="' + esc(a.variableName || '') + '" onchange="updateActionProp(' + i + ', \'variableName\', this.value)"></div>';
        } else if (a.type === 'logMessage') {
            html += '<div class="prop-row"><span class="prop-label">消息</span><input class="prop-input" type="text" value="' + esc(a.message || '') + '" onchange="updateActionProp(' + i + ', \'message\', this.value)"></div>';
        } else if (a.type === 'switchPhase') {
            html += '<div class="prop-row"><span class="prop-label">目标阶段</span><input class="prop-input" type="text" value="' + esc(a.targetPhase || '') + '" onchange="updateActionProp(' + i + ', \'targetPhase\', this.value)"></div>';
            html += '<div class="prop-row"><span class="prop-label">标签</span><input class="prop-input" type="text" value="' + esc(a.label || '') + '" onchange="updateActionProp(' + i + ', \'label\', this.value)"></div>';
            html += '<button class="btn" style="margin-top:2px;font-size:10px;padding:2px 8px" onclick="createPhaseForAction(' + i + ')">＋ 新建阶段并关联</button>';
        } else if (a.type === 'switchBranch') {
            html += '<div class="prop-row"><span class="prop-label">条件变量</span><input class="prop-input" type="text" value="' + esc(a.condition || '') + '" onchange="updateActionProp(' + i + ', \'condition\', this.value)"></div>';
            html += '<div class="prop-row"><span class="prop-label">目标分支</span><input class="prop-input" type="text" value="' + esc(a.targetBranch || '') + '" onchange="updateActionProp(' + i + ', \'targetBranch\', this.value)"></div>';
            html += '<button class="btn" style="margin-top:2px;font-size:10px;padding:2px 8px" onclick="createBranchForAction(' + i + ')">＋ 新建分支并关联</button>';
        }

        // 删除按钮
        html += '<button class="btn danger" style="margin-top:4px;" onclick="deleteAction(' + i + ')">×</button>';
        html += '</div>'; // end action-item
    }

    // 添加动作按钮
    html += '<button class="btn" style="margin-top:4px;" onclick="addAction()">+ 添加动作</button>';

    html += '</div>'; // end 动作

    // ==================== 删除事件 ====================
    html += '<button class="btn danger" style="width:100%;" onclick="deleteEvent()">删除事件</button>';

    panel.innerHTML = html;
}

// ==================== 属性面板事件处理器 ====================

function updateEventProp(field, value) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    ev[field] = value;
    markDirty();
}

function updateEventNumProp(field, value) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    ev[field] = parseFloat(value) || 0;
    markDirty();
}

function addSync(side) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    ev[side] = { windowBefore: 2.5, windowAfter: 2.5 };
    markDirty();
}

function removeSync(side) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    delete ev[side];
    markDirty();
}

function updateSyncNumProp(side, field, value) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev[side]) return;
    var num = parseFloat(value);
    ev[side][field] = isNaN(num) ? undefined : num;
    markDirty();
}

function updateSyncBoolProp(side, field, value) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev[side]) return;
    ev[side][field] = !!value;
    markDirty();
}

function updateTargetable(val) {
    if (!selectedEventPath) return;
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    if (val === '') ev.targetable = null;
    else if (val === 'true') ev.targetable = true;
    else ev.targetable = false;
    markDirty();
}

var TYPE_NAMES = { demand:'需求', skillSuggestion:'技能建议', setVariable:'设置变量', toggleVariable:'切换变量', logMessage:'日志', switchPhase:'切换阶段', switchBranch:'切换分支' };

function updateActionType(idx, newType) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions) return;
    ev.actions[idx] = JSON.parse(JSON.stringify(ACTION_TEMPLATES[newType]));
    ev.name = TYPE_NAMES[newType] || '新事件';
    // 切换阶段默认指向父阶段编号 +1
    if (newType === 'switchPhase') {
        var curPhase = timelineData.phases[currentPhaseIdx];
        var curNum = parseInt((curPhase.id || 'p1').substring(1)) || currentPhaseIdx + 1;
        ev.actions[idx].targetPhase = 'p' + (curNum + 1);
    }
    markDirty();
}

function updateActionProp(idx, field, value) {
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions || !ev.actions[idx]) return;
    ev.actions[idx][field] = value;
    markDirty();
}

function deleteAction(idx) {
    if (!confirm('确定删除此动作?')) return;
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions) return;
    ev.actions.splice(idx, 1);
    markDirty();
}

function addAction() {
    var ev = getEventByPath(selectedEventPath);
    if (!ev) return;
    if (!ev.actions) ev.actions = [];
    ev.actions.push(JSON.parse(JSON.stringify(ACTION_TEMPLATES.skillSuggestion)));
    markDirty();
}

/** 为 switchPhase 动作新建阶段并关联 */
function createPhaseForAction(actionIdx) {
    if (!timelineData || !timelineData.phases) return;
    if (timelineData.phases.length >= MAX_PHASES) { showError('已达阶段上限 (' + MAX_PHASES + ')'); return; }
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions || !ev.actions[actionIdx]) return;
    var newNum = findLowestPhaseNum();
    var newPhase = { id: 'p' + newNum, name: 'P' + newNum, events: [], switch: null };
    timelineData.phases.push(newPhase);
    ev.actions[actionIdx].targetPhase = newPhase.id;
    markDirty();
}

/** 为 switchBranch 动作新建分支并关联 */
function createBranchForAction(actionIdx) {
    var phase = getPhase(currentPhaseIdx);
    if (!phase) return;
    if (!phase.switch) {
        phase.switch = { sync: { windowBefore: 2.5, windowAfter: 2.5 }, branches: [] };
    }
    var ev = getEventByPath(selectedEventPath);
    if (!ev || !ev.actions || !ev.actions[actionIdx]) return;
    var newBranch = { name: '分支' + (phase.switch.branches.length + 1), events: [] };
    phase.switch.branches.push(newBranch);
    ev.actions[actionIdx].targetBranch = newBranch.name;
    markDirty();
}

function deleteEvent() {
    // 双击确认删除（1s 内连续点击两次）
    var now = Date.now();
    var last = eventDeleteTimers[selectedEventPath] || 0;
    if (now - last < 1000) {
        var info = getParentInfo(selectedEventPath);
        if (!info) return;
        info.container.splice(info.idx, 1);
        selectedEventPath = null;
        delete eventDeleteTimers[selectedEventPath];
        markDirty();
    } else {
        eventDeleteTimers[selectedEventPath] = now;
    }
}

function updateFooter() {
    var el = document.getElementById('footer');
    if (el) el.textContent = currentFile ? currentFile + (isDirty ? ' (未保存)' : '') : '就绪';
}

function showError(msg) {
    var el = document.getElementById('footer');
    if (!el) return;
    el.textContent = msg;
    el.style.color = 'var(--red)';
    setTimeout(function() {
        el.style.color = '';
        updateFooter();
    }, 3000);
}

function showLoading(msg) {
    var el = document.getElementById('footer');
    if (!el) return;
    el.textContent = msg;
    el.style.color = '';
}

// ==================== 文件操作 ====================

function newFile() {
    if (timelineData && isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    timelineData = newTimeline();
    currentFile = '';
    fileHandle = null;
    isDirty = false;
    currentPhaseIdx = 0;
    selectedEventPath = null;
    renderAll();
    syncFactMeta();
    updateFooter();
}

function loadFile() {
    if (timelineData && isDirty && !confirm('当前有未保存的修改，是否放弃？')) return;
    document.getElementById('fileInput').click();
}

function readFileObj(file) {
    showLoading('加载中...');
    var reader = new FileReader();
    reader.onload = function() {
        try {
            var data = JSON.parse(reader.result);
            timelineData = data;
            currentFile = file.name;
            fileHandle = null;
            isDirty = false;
            currentPhaseIdx = 0;
            selectedEventPath = null;
            renderAll();
            syncFactMeta();
            updateFooter();
        } catch (ex) {
            showError('JSON解析失败: ' + esc(ex.message));
        }
    };
    reader.onerror = function() {
        showError('文件读取失败');
    };
    reader.readAsText(file);
}

async function saveFile() {
    if (!timelineData) return;
    var json = JSON.stringify(timelineData, null, 2);
    if (fileHandle && typeof fileHandle.createWritable === 'function') {
        try {
            var writable = await fileHandle.createWritable();
            await writable.write(json);
            await writable.close();
            isDirty = false;
            updateFooter();
            return;
        } catch (e) {
            // 降级到 Save As
        }
    }
    saveFileAs();
}

async function saveFileAs() {
    if (!timelineData) return;
    var json = JSON.stringify(timelineData, null, 2);
    var name = currentFile || 'timeline.json';
    if (window.showSaveFilePicker) {
        try {
            var handle = await window.showSaveFilePicker({
                suggestedName: name,
                types: [{ description: 'JSON', accept: { 'application/json': ['.json'] } }]
            });
            var writable = await handle.createWritable();
            await writable.write(json);
            await writable.close();
            fileHandle = handle;
            currentFile = handle.name;
            isDirty = false;
            updateFooter();
            return;
        } catch (e) {
            if (e.name === 'AbortError') return;
        }
    }
    downloadJson(json, name);
}

function exportFile() {
    if (!timelineData) return;
    var json = JSON.stringify(timelineData, null, 2);
    var name = currentFile || 'timeline.json';
    downloadJson(json, name);
}

function downloadJson(json, name) {
    var blob = new Blob([json], { type: 'application/json' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = name;
    a.click();
    URL.revokeObjectURL(url);
}

// ==================== 拖拽 (垂直时间调整) ====================

function bindDragHandlers() {
    if (_dragHandlersBound) return;
    _dragHandlersBound = true;

    var tracksEl = document.getElementById('phaseTracks');
    if (!tracksEl) return;

    // -- 创建 drop indicator (复用) --
    var dropIndicator = document.createElement('div');
    dropIndicator.className = 'drop-indicator';
    dropIndicator.style.display = 'none';
    tracksEl.appendChild(dropIndicator);

    // -- mousedown: 事件代理在 #phaseTracks --
    tracksEl.addEventListener('mousedown', function(e) {
        if (e.button !== 0) return; // 只响应左键
        var node = e.target.closest('.event-node, .sub-branch-event');
        if (!node) return;
        dragState.active = true;
        dragState.srcPath = node.dataset.path;
        dragState.startY = e.clientY;
        dragState.moved = false;
        e.preventDefault(); // 防止文本选中
    });

    // -- mousemove: 全局 --
    document.addEventListener('mousemove', function(e) {
        if (!dragState.active) return;
        if (dragState.moved) return; // 已经移动过, 跳过重复计算
        if (Math.abs(e.clientY - dragState.startY) < 4) return; // 阈值

        dragState.moved = true;

        // 给源节点添加 .dragging 类
        var srcNode = document.querySelector('[data-path="' + dragState.srcPath + '"]');
        if (srcNode) {
            srcNode.classList.add('dragging');
        }

        // 显示 drop indicator
        dropIndicator.style.display = 'block';
    });

    // -- mouseup: 全局 --
    document.addEventListener('mouseup', function(e) {
        if (!dragState.active) return;

        var wasMoved = dragState.moved;
        var srcPath = dragState.srcPath;

        // 重置拖拽状态
        var srcNode = document.querySelector('[data-path="' + srcPath + '"]');
        if (srcNode) {
            srcNode.classList.remove('dragging');
        }
        dropIndicator.style.display = 'none';
        dragState.active = false;
        dragState.srcPath = null;
        dragState.moved = false;

        if (!wasMoved) return;

        // 计算新时间
        var canvasEl = document.getElementById('timelineCanvas');
        if (!canvasEl) return;
        var rect = canvasEl.getBoundingClientRect();
        var scrollEl2 = canvasEl.querySelector('.timeline-scroll');
        var scrollTop = scrollEl2 ? scrollEl2.scrollTop : 0;
        var rawTime = (e.clientY - rect.top + scrollTop) / LINE_HEIGHT * TIME_STEP + MIN_TIME;
        var newTime = Math.max(MIN_TIME, Math.min(MAX_TIME, Math.round(rawTime / TIME_STEP) * TIME_STEP));

        // 解析事件并更新时间
        var ev = getEventByPath(srcPath);
        if (!ev) return;
        ev.time = newTime;
        markDirty();

        // 防止后续 click 事件误触发
        dragState.preventClick = true;
        setTimeout(function() { dragState.preventClick = false; }, 50);
    });
}

// ==================== 右键菜单 ====================

/**
 * 在 (x, y) 处显示右键菜单
 * @param {number} x 屏幕 x 坐标 (clientX)
 * @param {number} y 屏幕 y 坐标 (clientY)
 * @param {Array} items 菜单项数组: { label, action, danger, separator }
 */
function showContextMenu(x, y, items) {
    var menu = document.getElementById('ctxMenu');
    if (!menu) return;

    var html = '';
    for (var i = 0; i < items.length; i++) {
        var item = items[i];
        if (item.separator) {
            html += '<div class="ctx-sep"></div>';
        } else {
            var cls = 'ctx-item' + (item.danger ? ' ctx-danger' : '');
            html += '<div class="' + cls + '" data-action="' + esc(item.action) + '">' + esc(item.label) + '</div>';
        }
    }
    menu.innerHTML = html;

    menu.style.left = x + 'px';
    menu.style.top = y + 'px';
    menu.classList.remove('hide');

    // 菜单项点击
    menu.onclick = function(e) {
        var target = e.target.closest('.ctx-item');
        if (!target) return;
        e.stopPropagation();
        handleContextAction(target.dataset.action);
    };
}

/** 隐藏右键菜单 */
function hideContextMenu() {
    var menu = document.getElementById('ctxMenu');
    if (!menu) return;
    menu.innerHTML = '';
    menu.classList.add('hide');
}

var eventDeleteTimers = {};  // 删除事件双击计时
var phaseDeleteClicks = {};  // 删除阶段三击计数

/** 处理菜单动作分发 */
function handleContextAction(action) {
    switch (action) {
        case 'addEvent': {
            var phase = getPhase(currentPhaseIdx);
            if (!phase) break;
            var newEv = {
                id: generateUUID(),
                name: '新事件',
                time: ctxMenuClickTime,
                duration: 0,
                actions: []
            };
            if (ctxMenuBranchIdx >= 0 && phase.switch && phase.switch.branches) {
                // 添加到分支
                var br = phase.switch.branches[ctxMenuBranchIdx];
                if (br && br.events) {
                    // 分支事件时间 = 绝对时间
                    newEv.time = Math.max(MIN_TIME, ctxMenuClickTime);
                    br.events.push(newEv);
                    selectedEventPath = 'p' + currentPhaseIdx + '_switch_br' + ctxMenuBranchIdx + '_ev' + (br.events.length - 1);
                }
            } else {
                phase.events.push(newEv);
                selectedEventPath = 'p' + currentPhaseIdx + '_ev' + (phase.events.length - 1);
            }
            markDirty();
            break;
        }
        case 'addAction': {
            // 给当前右键目标事件添加一个默认动作
            var ev = getEventByPath(ctxMenuTargetPath);
            if (!ev) break;
            if (!ev.actions) ev.actions = [];
            ev.actions.push({
                type: 'demand',
                '需求减伤': 0,
                '需求治疗': 0
            });
            markDirty();
            break;
        }
        case 'deleteEvent': {
            // 双击确认删除（1s 内连续点击两次删除）
            var now = Date.now();
            var last = eventDeleteTimers[ctxMenuTargetPath] || 0;
            if (now - last < 1000) {
                var info = getParentInfo(ctxMenuTargetPath);
                if (!info) break;
                info.container.splice(info.idx, 1);
                selectedEventPath = null;
                delete eventDeleteTimers[ctxMenuTargetPath];
                markDirty();
            } else {
                eventDeleteTimers[ctxMenuTargetPath] = now;
            }
            break;
        }
    }
    hideContextMenu();
    if (action === 'addEvent') frozenMarkerTime = null; // 添加事件后解锁
}

// ==================== 页面初始化 ====================

document.addEventListener('DOMContentLoaded', function() {
    if (!timelineData) {
        timelineData = newTimeline();
    }
    renderAll();
    syncFactMeta();
    updateFooter();

    ['factName', 'factTerritoryId', 'factAuthor', 'factNote'].forEach(function(id) {
        var el = document.getElementById(id);
        if (!el) return;
        el.addEventListener('change', function() {
            if (!timelineData) return;
            if (id === 'factName') timelineData.name = this.value;
            if (id === 'factTerritoryId') timelineData.territoryId = parseInt(this.value) || 0;
            if (id === 'factAuthor') timelineData.author = this.value;
            if (id === 'factNote') timelineData.note = this.value;
            markDirty();
        });
    });

    // ---- 文件操作绑定 ----

    document.getElementById('btnNew').addEventListener('click', newFile);
    document.getElementById('btnLoad').addEventListener('click', loadFile);
    document.getElementById('btnSave').addEventListener('click', saveFile);
    document.getElementById('btnExport').addEventListener('click', exportFile);

    document.getElementById('btnRecording').addEventListener('click', function() {
        document.getElementById('recordingFileInput').click();
    });
    document.getElementById('recordingFileInput').addEventListener('change', function() {
        if (this.files && this.files[0]) {
            loadRecordingFile(this.files[0]);
            this.value = '';
        }
    });
    document.getElementById('btnRecordingClose').addEventListener('click', hideRecordingPanel);
    document.getElementById('btnImportToPhase').addEventListener('click', importRecordingToPhase);
    document.getElementById('fileInput').addEventListener('change', function() {
        if (this.files && this.files[0]) {
            readFileObj(this.files[0]);
            this.value = '';
        }
    });

    // ---- 右键菜单事件绑定 ----

    // 画布空白处右键 → 添加事件
    var canvas = document.getElementById('timelineCanvas');
    if (canvas) {
        canvas.addEventListener('contextmenu', function(e) {
            e.preventDefault();
            e.stopPropagation();

            // 检测是否在分支区域上右键
            var subBranch = e.target.closest('.sub-branch');
            ctxMenuBranchIdx = -1;
            if (subBranch && subBranch.dataset.branchIdx !== undefined) {
                ctxMenuBranchIdx = parseInt(subBranch.dataset.branchIdx);
            }

            // 冻结鼠标横线到当前时间位置
            frozenMarkerTime = currentMouseTime;

            // 右键添加事件使用冻结的鼠标时间
            ctxMenuClickTime = Math.max(MIN_TIME, Math.min(MAX_TIME, frozenMarkerTime !== null ? frozenMarkerTime : 0));

            var label = (ctxMenuBranchIdx >= 0) ? '添加分支事件' : '添加事件';
            showContextMenu(e.clientX, e.clientY, [
                { label: label, action: 'addEvent' }
            ]);
        });
    }

    // 事件节点/子分支事件右键 → 添加动作 / 删除事件
    var tracks = document.getElementById('phaseTracks');
    if (tracks) {
        tracks.addEventListener('contextmenu', function(e) {
            var node = e.target.closest('.event-node, .sub-branch-event');
            if (!node) return;

            e.preventDefault();
            e.stopPropagation();

            // 冻结鼠标横线
            frozenMarkerTime = currentMouseTime;

            ctxMenuTargetPath = node.dataset.path;

            // 自动选中该事件
            selectedEventPath = ctxMenuTargetPath;
            var allNodes = tracks.querySelectorAll('.event-node, .sub-branch-event');
            for (var i = 0; i < allNodes.length; i++) {
                allNodes[i].classList.remove('sel');
            }
            node.classList.add('sel');
            renderProps();

            showContextMenu(e.clientX, e.clientY, [
                { label: '添加动作', action: 'addAction' },
                { label: '删除事件', action: 'deleteEvent', danger: true }
            ]);
        });
    }

    // 左键点击 phase track / branch 区域 → 选中高亮
    if (tracks) {
        tracks.addEventListener('click', function(e) {
            if (dragState.preventClick) return;
            // 不拦截事件节点点击（事件节点有自己的点击处理）
            if (e.target.closest('.event-node') || e.target.closest('.sub-branch-event')) return;
            // 点击分支区域
            var subBranch = e.target.closest('.sub-branch');
            if (subBranch) {
                document.querySelectorAll('.sub-branch.sel-phase').forEach(function(s) { s.classList.remove('sel-phase'); });
                subBranch.classList.add('sel-phase');
                return;
            }
            // 点击相位轨道区域
            var phaseTrack = e.target.closest('.phase-track');
            if (phaseTrack) {
                document.querySelectorAll('.phase-track.sel-phase').forEach(function(p) { p.classList.remove('sel-phase'); });
                phaseTrack.classList.add('sel-phase');
                var idx = parseInt(phaseTrack.dataset.phaseIdx);
                if (!isNaN(idx) && idx !== currentPhaseIdx) {
                    switchPhase(idx);
                }
            }
        });
    }

    // 点击菜单外部任意位置 → 关闭菜单；左键点击 → 解锁横线
    document.addEventListener('mousedown', function(e) {
        // 左键点击任意位置解锁横线
        if (e.button === 0) frozenMarkerTime = null;
        var menu = document.getElementById('ctxMenu');
        if (!menu || menu.classList.contains('hide')) return;
        if (!menu.contains(e.target)) {
            hideContextMenu();
        }
    });

    // ---- 鼠标时间标线 ----
    var mouseMarker = document.getElementById('mouseMarker');
    var mouseMarkerLabel = mouseMarker ? mouseMarker.querySelector('.mouse-marker-label') : null;
    var scrollEl = canvas ? canvas.querySelector('.timeline-scroll') : null;
    var currentMouseTime = 0;
    if (canvas) {
        canvas.addEventListener('mousemove', function(e) {
            if (dragState.active && dragState.moved) return;
            if (!mouseMarker) return;
            var rect = canvas.getBoundingClientRect();
            var scrollTop = scrollEl ? scrollEl.scrollTop : 0;
            var y = e.clientY - rect.top + scrollTop;
            currentMouseTime = y / LINE_HEIGHT * TIME_STEP + MIN_TIME;
            currentMouseTime = Math.round(currentMouseTime * 10) / 10;
            // 正常追踪 或 冻结显示
            var displayTime = (frozenMarkerTime !== null) ? frozenMarkerTime : currentMouseTime;
            var displayY = (frozenMarkerTime !== null)
                ? timeToY(frozenMarkerTime) - scrollTop
                : (e.clientY - rect.top);
            mouseMarker.classList.remove('hide');
            mouseMarker.style.top = displayY + 'px';
            if (mouseMarkerLabel) {
                mouseMarkerLabel.textContent = formatTime(displayTime) + ' (' + Math.round(displayY) + 'px, scroll=' + scrollTop + ')';
            }
        });
        canvas.addEventListener('mouseleave', function() {
            if (frozenMarkerTime === null && mouseMarker) mouseMarker.classList.add('hide');
        });
    }

    // ==================== 键盘快捷键 ====================

    document.addEventListener('keydown', function(e) {
        // Ctrl+S / Cmd+S → 触发生成器的保存按钮
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            var saveBtn = document.getElementById('btnSave');
            if (saveBtn) saveBtn.click();
            return;
        }

        // Delete → 删除选中事件（跳过编辑框）
        if (e.key === 'Delete' && selectedEventPath) {
            var tag = e.target.tagName;
            if (tag === 'INPUT' || tag === 'TEXTAREA') return;
            e.preventDefault();
            // 双击 Delete 确认删除
            var now = Date.now();
            var last = eventDeleteTimers[selectedEventPath] || 0;
            if (now - last < 1000) {
                var info = getParentInfo(selectedEventPath);
                if (!info) return;
                info.container.splice(info.idx, 1);
                selectedEventPath = null;
                delete eventDeleteTimers[selectedEventPath];
                markDirty();
            } else {
                eventDeleteTimers[selectedEventPath] = now;
            }
            return;
        }

        // Escape → 关闭右键菜单，清除事件选中
        if (e.key === 'Escape') {
            hideContextMenu();
            if (selectedEventPath) {
                selectedEventPath = null;
                var tracks = document.getElementById('phaseTracks');
                if (tracks) {
                    tracks.querySelectorAll('.event-node, .sub-branch-event').forEach(function(n) {
                        n.classList.remove('sel');
                    });
                }
                renderProps();
            }
            return;
        }
    });
});

// ==================== 录制导入 ====================

function loadRecordingFile(file) {
    showLoading('加载录制文件...');
    var reader = new FileReader();
    reader.onload = function() {
        try {
            recordingData = JSON.parse(reader.result);
            recordingChecked = {};
            for (var i = 0; i < (recordingData.events || []).length; i++) {
                recordingChecked[i] = false;
            }
            showRecordingPanel();
            setStatus('已加载: ' + esc(recordingData.territoryName || '未知'), 'success');
        } catch (ex) {
            setStatus('录制文件解析失败: ' + esc(ex.message), 'error');
        }
    };
    reader.onerror = function() {
        setStatus('文件读取失败', 'error');
    };
    reader.readAsText(file);
}

function showRecordingPanel() {
    if (!recordingData) return;
    var propPanel = document.getElementById('propPanel');
    var recPanel = document.getElementById('recordingPanel');
    if (propPanel) propPanel.style.display = 'none';
    if (recPanel) recPanel.style.display = '';
    renderRecordingContent();
}

function renderRecordingContent() {
    if (!recordingData) return;

    var info = document.getElementById('recordingInfo');
    if (info) {
        var durationSec = (recordingData.totalTimeMs || 0) / 1000;
        info.innerHTML =
            '<div><strong>' + esc(recordingData.territoryName || '未知副本') + '</strong></div>' +
            '<div style="font-size:10px;color:var(--tx2)">' +
            '时长: ' + formatTime(durationSec) +
            ' | 事件: ' + (recordingData.events ? recordingData.events.length : 0) +
            ' | 队伍: ' + (recordingData.jobComposition || []).join(', ') +
            '</div>';
    }

    var filtersEl = document.getElementById('recordingFilters');
    if (filtersEl) {
        var catNames = {
            cast: '读条技能', ability: '技能效果', buff: 'Buff',
            tether: '连线', spawn: '生成/消失', death: '死亡',
            target: '点名标记', combat: '战斗状态',
            environment: '环境特效', npc: 'NPC喊话',
            director: '时间轴/导演', actorControl: 'ActorControl', chat: '聊天'
        };
        var html = '<div style="font-size:11px;margin-bottom:4px;color:var(--tx1)">按分类过滤:</div>';
        for (var cat in recordingFilters) {
            var checked = recordingFilters[cat] ? ' checked' : '';
            html += '<label style="margin-right:8px;font-size:10px;cursor:pointer;white-space:nowrap">' +
                '<input type="checkbox" ' + checked +
                ' onchange="toggleRecordingFilter(\'' + cat + '\')">' +
                (catNames[cat] || cat) + '</label>';
        }
        filtersEl.innerHTML = html;
    }

    var eventsEl = document.getElementById('recordingEvents');
    if (eventsEl) {
        var filtered = filterRecordingEvents();
        if (filtered.length === 0) {
            eventsEl.innerHTML = '<div class="hint">没有匹配的事件</div>';
        } else {
            var listHtml = '';
            for (var i = 0; i < filtered.length; i++) {
                var ev = filtered[i];
                var origIdx = recordingData.events.indexOf(ev);
                var chk = recordingChecked[origIdx] ? ' checked' : '';
                var actionId = ev.data.actionId || ev.data.spellId || ev.data.statusId || ev.data.tetherId || '';
                var label = ev.category + (actionId ? ' #' + actionId : '') + ' @' + formatTime(ev.timeMs / 1000);
                if (ev.data.sourceName) label += ' [' + esc(String(ev.data.sourceName).substring(0, 15)) + ']';

                listHtml += '<div class="rec-event-row">' +
                    '<input type="checkbox"' + chk +
                    ' onchange="recordingChecked[' + origIdx + '] = this.checked">' +
                    '<span class="rec-event-label">' + esc(label) + '</span></div>';
            }
            eventsEl.innerHTML = listHtml;
        }
    }
}

function toggleRecordingFilter(cat) {
    recordingFilters[cat] = !recordingFilters[cat];
    renderRecordingContent();
}

function filterRecordingEvents() {
    if (!recordingData || !recordingData.events) return [];
    return recordingData.events.filter(function(ev) {
        return recordingFilters[ev.category];
    });
}

function hideRecordingPanel() {
    recordingData = null;
    recordingChecked = {};
    var propPanel = document.getElementById('propPanel');
    var recPanel = document.getElementById('recordingPanel');
    if (propPanel) propPanel.style.display = '';
    if (recPanel) recPanel.style.display = 'none';
    renderProps();
    updateFooter();
}

function importRecordingToPhase() {
    if (!recordingData) return;
    var phase = getPhase(currentPhaseIdx);
    if (!phase) { setStatus('请先选择一个阶段', 'error'); return; }

    var selected = [];
    for (var idx in recordingChecked) {
        if (recordingChecked[idx]) {
            selected.push(recordingData.events[parseInt(idx)]);
        }
    }
    if (selected.length === 0) {
        setStatus('请先勾选要导入的事件', 'error');
        return;
    }

    selected.sort(function(a, b) { return a.timeMs - b.timeMs; });

    var imported = 0;
    for (var s = 0; s < selected.length; s++) {
        var ev = selected[s];
        var timeSec = ev.timeMs / 1000;

        var actionId = ev.data.actionId || ev.data.spellId ||
                       ev.data.statusId || ev.data.abilityId || 0;

        var name = '[' + ev.category + '] ';
        if (ev.data.sourceName) name += esc(String(ev.data.sourceName).substring(0, 20)) + ' ';
        if (actionId) name += '#' + actionId;

        var fe = {
            id: generateUUID(),
            name: name,
            time: timeSec,
            duration: 0,
            actions: []
        };

        if (actionId) {
            fe.startSync = {
                type: 'ability',
                abilityIds: [parseInt(actionId)]
            };
        }

        var dup = false;
        for (var ei = 0; ei < phase.events.length; ei++) {
            var existing = phase.events[ei];
            if (Math.abs(existing.time - timeSec) < 0.01 && existing.name === name) {
                dup = true;
                break;
            }
        }
        if (dup) continue;

        phase.events.push(fe);
        imported++;
    }

    phase.events.sort(function(a, b) { return a.time - b.time; });

    setStatus('已导入 ' + imported + ' 个事件, 跳过 ' +
              (selected.length - imported) + ' 个重复', 'success');
    hideRecordingPanel();
    markDirty();
}
