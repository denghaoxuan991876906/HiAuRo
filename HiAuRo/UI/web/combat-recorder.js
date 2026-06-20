(function () {
    const state = {
        file: null,
        fileHandle: null,
        fileName: "",
        warnings: [],
        rows: [],
        originalRows: [],
        groups: [],
        selectedGroupId: null,
        trim: {
            startGroupId: null,
            endGroupId: null,
            previewActive: false
        },
        filters: {
            eventType: "all",
            search: "",
            changedOnly: false,
            showRawJson: false
        }
    };

    const refs = {
        fileInput: document.getElementById("fileInput"),
        btnChoose: document.getElementById("btnChoose"),
        btnReload: document.getElementById("btnReload"),
        btnSetStart: document.getElementById("btnSetStart"),
        btnSetEnd: document.getElementById("btnSetEnd"),
        btnPreviewTrim: document.getElementById("btnPreviewTrim"),
        btnResetTrim: document.getElementById("btnResetTrim"),
        btnSaveTrim: document.getElementById("btnSaveTrim"),
        fileName: document.getElementById("fileName"),
        trimStartInfo: document.getElementById("trimStartInfo"),
        trimEndInfo: document.getElementById("trimEndInfo"),
        trimStatus: document.getElementById("trimStatus"),
        filterType: document.getElementById("filterType"),
        searchInput: document.getElementById("searchInput"),
        changedOnly: document.getElementById("changedOnly"),
        showRawJson: document.getElementById("showRawJson"),
        statGroups: document.getElementById("statGroups"),
        statGcd: document.getElementById("statGcd"),
        statAbility: document.getElementById("statAbility"),
        statWarnings: document.getElementById("statWarnings"),
        timelineSubtitle: document.getElementById("timelineSubtitle"),
        detailsSubtitle: document.getElementById("detailsSubtitle"),
        timelineList: document.getElementById("timelineList"),
        detailsContent: document.getElementById("detailsContent"),
        footerText: document.getElementById("footerText")
    };

    bindEvents();
    render();

    function bindEvents() {
        refs.btnChoose.addEventListener("click", async () => {
            if (window.showOpenFilePicker) {
                try {
                    const handles = await window.showOpenFilePicker({
                        types: [{ description: "JSONL", accept: { "application/json": [".jsonl", ".txt"] } }]
                    });
                    const handle = handles[0];
                    const file = await handle.getFile();
                    await loadFile(file, handle);
                    return;
                } catch (error) {
                    if (error && error.name === "AbortError") return;
                }
            }
            refs.fileInput.click();
        });

        refs.btnReload.addEventListener("click", async () => {
            if (!state.file) return;
            await loadFile(state.file, state.fileHandle);
        });

        refs.fileInput.addEventListener("change", async (event) => {
            const file = event.target.files && event.target.files[0];
            if (!file) return;
            await loadFile(file, null);
        });

        refs.btnSetStart.addEventListener("click", () => {
            if (!state.selectedGroupId) return;
            state.trim.startGroupId = state.selectedGroupId;
            updateTrimUi();
        });

        refs.btnSetEnd.addEventListener("click", () => {
            if (!state.selectedGroupId) return;
            state.trim.endGroupId = state.selectedGroupId;
            updateTrimUi();
        });

        refs.btnPreviewTrim.addEventListener("click", () => {
            if (!canPreviewTrim()) return;
            applyTrimPreview();
        });

        refs.btnResetTrim.addEventListener("click", () => resetTrimPreview());

        refs.btnSaveTrim.addEventListener("click", async () => {
            if (!state.trim.previewActive || !state.fileHandle) return;
            if (!window.confirm("会直接覆盖当前原始日志文件，只保留当前预览范围内的数据。确认继续吗？")) return;
            await saveTrimmedFile();
        });

        refs.filterType.addEventListener("change", () => {
            state.filters.eventType = refs.filterType.value;
            ensureSelectedGroupVisible();
            render();
        });

        refs.searchInput.addEventListener("input", () => {
            state.filters.search = refs.searchInput.value.trim();
            ensureSelectedGroupVisible();
            render();
        });

        refs.changedOnly.addEventListener("change", () => {
            state.filters.changedOnly = refs.changedOnly.checked;
            ensureSelectedGroupVisible();
            render();
        });

        refs.showRawJson.addEventListener("change", () => {
            state.filters.showRawJson = refs.showRawJson.checked;
            renderDetails();
        });

        window.addEventListener("keydown", (event) => {
            if (!state.groups.length) return;
            if (event.key !== "ArrowUp" && event.key !== "ArrowDown") return;

            const groups = getFilteredGroups();
            if (!groups.length) return;

            const currentIndex = groups.findIndex(g => g.eventGroupId === state.selectedGroupId);
            const offset = event.key === "ArrowUp" ? -1 : 1;
            const nextIndex = Math.min(groups.length - 1, Math.max(0, (currentIndex < 0 ? 0 : currentIndex) + offset));
            state.selectedGroupId = groups[nextIndex].eventGroupId;
            render();
            event.preventDefault();
        });
    }

    async function loadFile(file, fileHandle) {
        state.file = file;
        state.fileHandle = fileHandle;
        state.fileName = file.name;
        const text = await file.text();
        const parsed = parseJsonl(text);
        state.warnings = parsed.warnings;
        state.originalRows = parsed.rows;
        state.rows = parsed.rows;
        state.groups = buildGroups(state.rows);
        state.selectedGroupId = state.groups.length ? state.groups[0].eventGroupId : null;
        state.trim.startGroupId = null;
        state.trim.endGroupId = null;
        state.trim.previewActive = false;
        refs.btnReload.disabled = false;
        ensureSelectedGroupVisible();
        updateTrimUi();
        render();
    }

    function parseJsonl(text) {
        const rows = [];
        const warnings = [];
        const lines = text.split(/\r?\n/);

        for (let index = 0; index < lines.length; index++) {
            const line = lines[index].trim();
            if (!line) continue;

            try {
                rows.push(JSON.parse(line));
            } catch (error) {
                warnings.push(`第 ${index + 1} 行解析失败: ${error.message}`);
            }
        }

        return { rows, warnings };
    }

    function buildGroups(rows) {
        const groupMap = new Map();

        rows.forEach((row, rowIndex) => {
            const groupId = row.eventGroupId || `__ungrouped_${rowIndex}`;
            let group = groupMap.get(groupId);
            if (!group) {
                group = {
                    eventGroupId: groupId,
                    sessionId: row.sessionId || "",
                    eventType: row.eventType || "Unknown",
                    actionId: row.actionId || 0,
                    actionName: row.actionName || `Action ${row.actionId || 0}`,
                    timestamp: row.timestamp || "",
                    prev: null,
                    current: null,
                    runnerDebug: null
                };
                groupMap.set(groupId, group);
            }

            if (row.sampleRole === "current") {
                if (!group.current) group.current = row;
                if (row.before && !group.prev) group.prev = row.before;
                if (row.runnerDebug && !group.runnerDebug) group.runnerDebug = row.runnerDebug;
                return;
            }

            if ((row.sampleRole === "before" || row.sampleRole === "prev") && !group.prev) {
                group.prev = row;
                return;
            }

            if (!group.current) {
                group.current = row;
                if (row.before && !group.prev) group.prev = row.before;
                if (row.runnerDebug && !group.runnerDebug) group.runnerDebug = row.runnerDebug;
            }
        });

        const groups = Array.from(groupMap.values()).map(group => enrichGroup(group));
        groups.sort((a, b) => {
            const ta = Date.parse(a.timestamp || a.current?.timestamp || a.prev?.timestamp || "") || 0;
            const tb = Date.parse(b.timestamp || b.current?.timestamp || b.prev?.timestamp || "") || 0;
            return ta - tb;
        });
        return groups;
    }

    function enrichGroup(group) {
        const snapshot = group.current || group.prev || {};
        const diff = buildDiff(group.prev, group.current);
        return {
            ...group,
            timestamp: snapshot.timestamp || group.timestamp,
            eventType: snapshot.eventType || group.eventType,
            actionId: snapshot.actionId || group.actionId,
            actionName: snapshot.actionName || group.actionName,
            diff,
            summaryText: buildSummaryText(group, diff),
            hasDiff: diff.hasAnyDiff
        };
    }

    function buildDiff(prev, current) {
        const scalarRows = [];
        const gaugeRows = [];

        const pushScalar = (label, prevValue, currentValue, formatter) => {
            const before = formatter(prevValue);
            const after = formatter(currentValue);
            const changed = before !== after;
            scalarRows.push({ label, before, after, changed });
            return changed;
        };

        let hasAnyDiff = false;
        hasAnyDiff = pushScalar("HP", prev?.hp, current?.hp, formatNumber) || hasAnyDiff;
        hasAnyDiff = pushScalar("MP", prev?.mp, current?.mp, formatNumber) || hasAnyDiff;
        hasAnyDiff = pushScalar("GCD", prev?.gcdCooldown, current?.gcdCooldown, formatMilliseconds) || hasAnyDiff;
        hasAnyDiff = pushScalar("GCD类型", prev?.gcdKind, current?.gcdKind, formatGcdKind) || hasAnyDiff;
        hasAnyDiff = pushScalar("读条", prev?.isCasting, current?.isCasting, formatBoolean) || hasAnyDiff;
        hasAnyDiff = pushScalar("移动", prev?.isMoving, current?.isMoving, formatBoolean) || hasAnyDiff;
        hasAnyDiff = pushScalar("连击ID", prev?.lastComboSpellId, current?.lastComboSpellId, formatNumber) || hasAnyDiff;
        hasAnyDiff = pushScalar("目标HP%", prev?.targetHpPercent, current?.targetHpPercent, formatPercent) || hasAnyDiff;
        hasAnyDiff = pushScalar("距离", prev?.distance, current?.distance, formatDistance) || hasAnyDiff;

        uniqueKeys(prev?.jobGauge, current?.jobGauge).forEach((key) => {
            const before = formatValue(prev?.jobGauge?.[key]);
            const after = formatValue(current?.jobGauge?.[key]);
            const changed = before !== after;
            gaugeRows.push({ label: key, before, after, changed });
            hasAnyDiff = hasAnyDiff || changed;
        });

        const selfBuffDiff = buildBuffDiff(prev?.selfBuffs, current?.selfBuffs);
        const targetBuffDiff = buildBuffDiff(prev?.targetBuffs, current?.targetBuffs);
        hasAnyDiff = hasAnyDiff || selfBuffDiff.hasDiff || targetBuffDiff.hasDiff;

        return {
            scalarRows,
            gaugeRows,
            selfBuffDiff,
            targetBuffDiff,
            hasAnyDiff
        };
    }

    function buildBuffDiff(prevBuffs, currentBuffs) {
        const prevMap = buildBuffMap(prevBuffs);
        const currMap = buildBuffMap(currentBuffs);
        const added = [];
        const removed = [];
        const changed = [];

        const keys = new Set([...prevMap.keys(), ...currMap.keys()]);
        keys.forEach((key) => {
            const before = prevMap.get(key);
            const after = currMap.get(key);
            if (!before && after) {
                added.push(`${after.name} (${after.id})`);
                return;
            }
            if (before && !after) {
                removed.push(`${before.name} (${before.id})`);
                return;
            }
            if (!before || !after) return;

            const changes = [];
            if ((before.stack || 0) !== (after.stack || 0))
                changes.push(`层数 ${before.stack || 0} -> ${after.stack || 0}`);
            if ((before.remainMs || 0) !== (after.remainMs || 0))
                changes.push(`剩余 ${formatMilliseconds(before.remainMs)} -> ${formatMilliseconds(after.remainMs)}`);
            if (changes.length)
                changed.push(`${after.name} (${after.id}) | ${changes.join(" | ")}`);
        });

        return {
            added,
            removed,
            changed,
            hasDiff: added.length > 0 || removed.length > 0 || changed.length > 0
        };
    }

    function buildSummaryText(group, diff) {
        const parts = [];
        const prev = group.prev;
        const current = group.current;

        if ((prev?.mp ?? null) !== (current?.mp ?? null))
            parts.push(`MP ${formatNumber(prev?.mp)} -> ${formatNumber(current?.mp)}`);

        for (const key of uniqueKeys(prev?.jobGauge, current?.jobGauge)) {
            const before = formatValue(prev?.jobGauge?.[key]);
            const after = formatValue(current?.jobGauge?.[key]);
            if (before !== after) {
                parts.push(`${key} ${before} -> ${after}`);
                break;
            }
        }

        const selfBuffChanges = diff.selfBuffDiff.added.length + diff.selfBuffDiff.removed.length + diff.selfBuffDiff.changed.length;
        if (selfBuffChanges > 0)
            parts.push(`selfBuff ${selfBuffChanges}项变化`);

        const targetBuffChanges = diff.targetBuffDiff.added.length + diff.targetBuffDiff.removed.length + diff.targetBuffDiff.changed.length;
        if (targetBuffChanges > 0)
            parts.push(`targetBuff ${targetBuffChanges}项变化`);

        if ((prev?.targetHpPercent ?? null) !== (current?.targetHpPercent ?? null))
            parts.push(`target ${formatPercent(prev?.targetHpPercent)} -> ${formatPercent(current?.targetHpPercent)}`);

        if (!parts.length)
            parts.push(diff.hasAnyDiff ? "存在差异" : "无关键差异");

        return parts.join(" | ");
    }

    function getFilteredGroups() {
        const search = state.filters.search.toLowerCase();
        return state.groups.filter((group) => {
            if (state.filters.eventType !== "all" && group.eventType !== state.filters.eventType)
                return false;
            if (state.filters.changedOnly && !group.hasDiff)
                return false;
            if (!search) return true;
            const haystack = `${group.actionName} ${group.actionId} ${group.eventType}`.toLowerCase();
            return haystack.includes(search);
        });
    }

    function ensureSelectedGroupVisible() {
        const groups = getFilteredGroups();
        if (!groups.length) {
            state.selectedGroupId = null;
            return;
        }

        if (!groups.some(group => group.eventGroupId === state.selectedGroupId))
            state.selectedGroupId = groups[0].eventGroupId;
    }

    function render() {
        renderStats();
        renderTimeline();
        renderDetails();
        refs.fileName.textContent = state.fileName || "未选择文件";
        updateTrimUi();
    }

    function renderStats() {
        const groups = getFilteredGroups();
        refs.statGroups.textContent = String(groups.length);
        refs.statGcd.textContent = String(groups.filter(g => g.eventType === "GcdReadyAndAction").length);
        refs.statAbility.textContent = String(groups.filter(g => g.eventType === "AbilityEffect").length);
        refs.statWarnings.textContent = String(state.warnings.length);
        refs.timelineSubtitle.textContent = state.fileName ? `${groups.length} 组事件` : "等待加载文件";
        refs.footerText.textContent = state.fileName
            ? `${state.fileName} | ${state.rows.length} 条记录 | ${state.warnings.length} 条警告`
            : "Combat Recorder Viewer";
    }

    function renderTimeline() {
        const groups = getFilteredGroups();
        if (!groups.length) {
            refs.timelineList.innerHTML = `
                <div class="empty-state">
                  <div class="empty-title">${state.fileName ? "没有符合条件的事件组" : "选择一份 Combat Recorder 日志"}</div>
                  <div class="empty-copy">${state.fileName ? "调整筛选条件，或关闭“只看有变化项”后再试。" : "页面会按 eventGroupId 聚合，并展示每次技能事件的 before/current 差异。"}</div>
                </div>
            `;
            return;
        }

        refs.timelineList.innerHTML = groups.map(group => renderTimelineRow(group)).join("");
        refs.timelineList.querySelectorAll(".event-row").forEach((element) => {
            element.addEventListener("click", () => {
                state.selectedGroupId = element.dataset.groupId;
                render();
            });
        });
    }

    function renderTimelineRow(group) {
        const badgeClass = group.eventType === "AbilityEffect" ? "ability" : group.eventType === "StallDetected" ? "diagnostic" : "gcd";
        const badgeText = group.eventType === "AbilityEffect" ? "Ability" : group.eventType === "StallDetected" ? "STALL" : "GCD";
        const selectedClass = group.eventGroupId === state.selectedGroupId ? "selected" : "";
        const isStart = state.trim.startGroupId === group.eventGroupId;
        const isEnd = state.trim.endGroupId === group.eventGroupId;
        const marker = isStart && isEnd ? "起/止" : isStart ? "起点" : isEnd ? "终点" : "";
        return `
            <button class="event-row ${selectedClass}" data-group-id="${escapeHtml(group.eventGroupId)}">
              <div class="event-head">
                <div class="event-title">
                  <span class="event-badge ${badgeClass}">${badgeText}</span>
                  <span class="event-name">${escapeHtml(group.actionName || `Action ${group.actionId}`)}</span>
                  ${marker ? `<span class="event-badge">${escapeHtml(marker)}</span>` : ""}
                </div>
                <span class="event-time">${escapeHtml(formatTime(group.timestamp))}</span>
              </div>
              <div class="event-summary">${escapeHtml(group.summaryText)}</div>
            </button>
        `;
    }

    function renderDetails() {
        const group = state.groups.find(item => item.eventGroupId === state.selectedGroupId);
        if (!group) {
            refs.detailsSubtitle.textContent = "未选择事件组";
            refs.detailsContent.innerHTML = `
                <div class="empty-state">
                  <div class="empty-title">从左侧选择一个事件组</div>
                  <div class="empty-copy">右侧会优先显示关键差异，再提供原始 JSON 兜底。</div>
                </div>
            `;
            return;
        }

        refs.detailsSubtitle.textContent = `${group.actionName} / ${group.eventType}`;
        const warningBlock = state.warnings.length
            ? `<div class="warning-banner">解析时跳过 ${state.warnings.length} 行异常记录。</div>`
            : "";

        refs.detailsContent.innerHTML = `
            ${warningBlock}
            ${renderOverview(group)}
            ${renderScalarSection(group)}
            ${renderTrackedSkillsSection(group)}
            ${renderGaugeSection(group)}
            ${renderBuffSection("自身 Buff 变化", group.diff.selfBuffDiff)}
            ${renderBuffSection("目标 Buff 变化", group.diff.targetBuffDiff)}
            ${renderRunnerDebugSection(group)}
            ${renderRawSection(group)}
        `;
    }

    function renderOverview(group) {
        const snapshot = group.current || group.prev || {};
        return `
            <div class="overview-strip">
              ${renderOverviewCard("技能", group.actionName)}
              ${renderOverviewCard("ActionId", String(group.actionId))}
              ${renderOverviewCard("事件", group.eventType)}
              ${renderOverviewCard("GCD类型", formatGcdKind(snapshot.gcdKind))}
              ${renderOverviewCard("来源", snapshot.source || "unknown")}
              ${renderOverviewCard("时间", formatTime(group.timestamp))}
              ${renderOverviewCard("事件组", group.eventGroupId)}
            </div>
        `;
    }

    function renderScalarSection(group) {
        const rows = group.diff.scalarRows.map((row) => `
            <tr class="${row.changed ? "changed" : ""}">
              <td class="label-cell">${escapeHtml(row.label)}</td>
              <td class="${row.changed ? "" : "value-muted"}">${escapeHtml(row.before)}</td>
              <td class="${row.changed ? "delta-text" : "value-muted"}">${escapeHtml(row.after)}</td>
            </tr>
        `).join("");

        return `
            <section class="detail-section">
              <div class="detail-heading">资源与战斗状态</div>
              <div class="detail-body">
                <table class="diff-table">
                  <thead>
                    <tr>
                      <th class="label-cell">字段</th>
                      <th>Before</th>
                      <th>Current</th>
                    </tr>
                  </thead>
                  <tbody>${rows}</tbody>
                </table>
              </div>
            </section>
        `;
    }

    function renderGaugeSection(group) {
        if (!group.diff.gaugeRows.length) {
            return `
                <section class="detail-section">
                  <div class="detail-heading">职业量谱</div>
                  <div class="detail-body"><div class="value-muted">该记录没有职业量谱字段。</div></div>
                </section>
            `;
        }

        const rows = group.diff.gaugeRows.map((row) => `
            <tr class="${row.changed ? "changed" : ""}">
              <td class="label-cell">${escapeHtml(row.label)}</td>
              <td class="${row.changed ? "" : "value-muted"}">${escapeHtml(row.before)}</td>
              <td class="${row.changed ? "delta-text" : "value-muted"}">${escapeHtml(row.after)}</td>
            </tr>
        `).join("");

        return `
            <section class="detail-section">
              <div class="detail-heading">职业量谱</div>
              <div class="detail-body">
                <table class="diff-table">
                  <thead>
                    <tr>
                      <th class="label-cell">字段</th>
                      <th>Before</th>
                      <th>Current</th>
                    </tr>
                  </thead>
                  <tbody>${rows}</tbody>
                </table>
              </div>
            </section>
        `;
    }

    function renderTrackedSkillsSection(group) {
        const snapshot = group.current || group.prev || {};
        const skills = snapshot.trackedSkills || [];
        if (!skills.length) {
            return `
                <section class="detail-section">
                  <div class="detail-heading">自定义技能</div>
                  <div class="detail-body"><div class="value-muted">该记录未附带自定义技能信息。</div></div>
                </section>
            `;
        }

        const rows = skills.map((skill) => `
            <tr>
              <td class="label-cell">${escapeHtml(skill.actionName || `Action ${skill.actionId || 0}`)}</td>
              <td>${escapeHtml(String(skill.actionId || 0))}</td>
              <td>${escapeHtml(`${Math.round(Number(skill.cooldownRemainingMs || 0))}ms`)}</td>
              <td>${escapeHtml(`${Number(skill.charges || 0)}/${Number(skill.maxCharges || 0)}`)}</td>
            </tr>
        `).join("");

        return `
            <section class="detail-section">
              <div class="detail-heading">自定义技能</div>
              <div class="detail-body">
                <table class="diff-table">
                  <thead>
                    <tr>
                      <th class="label-cell">技能</th>
                      <th>ID</th>
                      <th>CD</th>
                      <th>层数</th>
                    </tr>
                  </thead>
                  <tbody>${rows}</tbody>
                </table>
              </div>
            </section>
        `;
    }

    function renderBuffSection(title, diff) {
        return `
            <section class="detail-section">
              <div class="detail-heading">${escapeHtml(title)}</div>
              <div class="detail-body buff-grid">
                ${renderListColumn("新增", diff.added, "added")}
                ${renderListColumn("移除", diff.removed, "removed")}
                ${renderListColumn("变化", diff.changed, "changed")}
              </div>
            </section>
        `;
    }

    function renderRunnerDebugSection(group) {
        if (!group.runnerDebug && !(group.current?.resolverResults || []).length)
            return "";

        const debug = group.runnerDebug || {};
        const resolvers = (group.current?.resolverResults || []).map((resolver) => `
            <tr>
              <th>${escapeHtml(resolver.name || "-")}</th>
              <td>${escapeHtml(resolver.mode || "-")}</td>
              <td>${escapeHtml(String(resolver.checkResult ?? "-"))}</td>
              <td>${escapeHtml(resolver.passedWindow ? "是" : "否")}</td>
            </tr>
        `).join("");

        return `
            <section class="detail-section">
              <div class="detail-heading">Runner Debug</div>
              <div class="detail-body">
                <table class="diff-table">
                  <tbody>
                    <tr><td class="label-cell">Phase</td><td>${escapeHtml(debug.phase || "-")}</td><td class="label-cell">SlotSource</td><td>${escapeHtml(debug.slotSource || "-")}</td></tr>
                    <tr><td class="label-cell">CanGcd</td><td>${escapeHtml(formatBoolean(debug.canGcd))}</td><td class="label-cell">CanOgcd</td><td>${escapeHtml(formatBoolean(debug.canOgcd))}</td></tr>
                    <tr><td class="label-cell">HasNextSlot</td><td>${escapeHtml(formatBoolean(debug.hasNextSlot))}</td><td class="label-cell">HasWaitGcdSlot</td><td>${escapeHtml(formatBoolean(debug.hasWaitGcdSlot))}</td></tr>
                    <tr><td class="label-cell">HasCurrSlot</td><td>${escapeHtml(formatBoolean(debug.hasCurrSlot))}</td><td class="label-cell"></td><td></td></tr>
                  </tbody>
                </table>
                ${resolvers ? `
                    <div class="detail-heading" style="margin-top:12px;">Resolver Results</div>
                    <table class="diff-table">
                      <thead>
                        <tr><th class="label-cell">Resolver</th><th>Mode</th><th>Check</th><th>Passed</th></tr>
                      </thead>
                      <tbody>${resolvers}</tbody>
                    </table>
                ` : `<div class="value-muted">该事件没有 resolver 结果。</div>`}
              </div>
            </section>
        `;
    }

    function renderRawSection(group) {
        if (!state.filters.showRawJson) return "";
        return `
            <details class="detail-section">
              <summary>原始 JSON</summary>
              <div class="detail-body two-col">
                <div>
                  <div class="detail-heading">before</div>
                  <pre class="raw-json">${escapeHtml(JSON.stringify(group.prev, null, 2) || "null")}</pre>
                </div>
                <div>
                  <div class="detail-heading">current</div>
                  <pre class="raw-json">${escapeHtml(JSON.stringify(group.current, null, 2) || "null")}</pre>
                </div>
              </div>
            </details>
        `;
    }

    function renderOverviewCard(label, value) {
        return `
            <div class="overview-card">
              <div class="overview-label">${escapeHtml(label)}</div>
              <div class="overview-value">${escapeHtml(value || "-")}</div>
            </div>
        `;
    }

    function renderListColumn(label, items, className) {
        const content = items.length
            ? `<div class="stack-list">${items.map(item => `<div class="stack-item ${className}">${escapeHtml(item)}</div>`).join("")}</div>`
            : `<div class="value-muted">无</div>`;
        return `
            <div>
              <div class="overview-label">${escapeHtml(label)}</div>
              ${content}
            </div>
        `;
    }

    function canPreviewTrim() {
        return !!state.trim.startGroupId && !!state.trim.endGroupId && state.originalRows.length > 0;
    }

    function applyTrimPreview() {
        if (!canPreviewTrim()) return;
        const bounds = getTrimBounds();
        state.rows = state.originalRows.filter((row) => {
            const time = Date.parse(row.timestamp || "") || 0;
            return time >= bounds.startTime && time <= bounds.endTime;
        });
        state.groups = buildGroups(state.rows);
        state.trim.previewActive = true;
        ensureSelectedGroupVisible();
        render();
    }

    function resetTrimPreview() {
        state.rows = state.originalRows.slice();
        state.groups = buildGroups(state.rows);
        state.trim.previewActive = false;
        ensureSelectedGroupVisible();
        render();
    }

    function getTrimBounds() {
        const ordered = buildGroups(state.originalRows);
        const startGroup = ordered.find((group) => group.eventGroupId === state.trim.startGroupId);
        const endGroup = ordered.find((group) => group.eventGroupId === state.trim.endGroupId);
        if (!startGroup || !endGroup)
            throw new Error("未找到截断范围边界事件组");

        const startTime = Math.min(
            Date.parse(startGroup.prev?.timestamp || "") || Number.MAX_SAFE_INTEGER,
            Date.parse(startGroup.current?.timestamp || "") || Number.MAX_SAFE_INTEGER
        );
        const endTime = Math.max(
            Date.parse(endGroup.prev?.timestamp || "") || 0,
            Date.parse(endGroup.current?.timestamp || "") || 0
        );

        return startTime <= endTime
            ? { startTime, endTime, startGroup, endGroup }
            : { startTime: endTime, endTime: startTime, startGroup: endGroup, endGroup: startGroup };
    }

    async function saveTrimmedFile() {
        const lines = state.rows.map((row) => JSON.stringify(row)).join("\n");
        const writer = await state.fileHandle.createWritable();
        await writer.write(lines + (lines ? "\n" : ""));
        await writer.close();
        const freshFile = await state.fileHandle.getFile();
        await loadFile(freshFile, state.fileHandle);
        refs.trimStatus.textContent = "已覆盖保存截断后的原文件";
    }

    function updateTrimUi() {
        refs.btnSetStart.disabled = !state.selectedGroupId;
        refs.btnSetEnd.disabled = !state.selectedGroupId;
        refs.btnPreviewTrim.disabled = !canPreviewTrim();
        refs.btnResetTrim.disabled = !state.trim.previewActive;
        refs.btnSaveTrim.disabled = !state.trim.previewActive || !state.fileHandle;

        refs.trimStartInfo.textContent = formatGroupSelection(state.trim.startGroupId);
        refs.trimEndInfo.textContent = formatGroupSelection(state.trim.endGroupId);

        if (state.trim.previewActive) {
            refs.trimStatus.textContent = state.fileHandle
                ? "当前为截断预览，可直接覆盖原文件"
                : "当前为截断预览；如需覆盖原文件，请用支持写权限的方式重新打开";
        } else if (canPreviewTrim()) {
            refs.trimStatus.textContent = "已选择范围，可预览截断结果";
        } else {
            refs.trimStatus.textContent = "未选择截断范围";
        }
    }

    function formatGroupSelection(groupId) {
        if (!groupId) return "未设置";
        const group = state.groups.find((item) => item.eventGroupId === groupId)
            || buildGroups(state.originalRows).find((item) => item.eventGroupId === groupId);
        if (!group) return groupId;
        return `${formatTime(group.timestamp)} / ${group.actionName} / ${group.eventType}`;
    }

    function buildBuffMap(buffs) {
        const map = new Map();
        (buffs || []).forEach((buff) => {
            if (!buff || !buff.id) return;
            map.set(String(buff.id), {
                id: buff.id,
                name: buff.name || `Buff ${buff.id}`,
                stack: buff.stack || 0,
                remainMs: buff.remainMs || 0
            });
        });
        return map;
    }

    function uniqueKeys(prev, current) {
        return Array.from(new Set([
            ...Object.keys(prev || {}),
            ...Object.keys(current || {})
        ]));
    }

    function formatNumber(value) {
        return value == null ? "-" : String(value);
    }

    function formatBoolean(value) {
        if (value == null) return "-";
        return value ? "是" : "否";
    }

    function formatPercent(value) {
        if (value == null || Number.isNaN(Number(value))) return "-";
        return `${(Number(value) * 100).toFixed(1)}%`;
    }

    function formatMilliseconds(value) {
        if (value == null || Number.isNaN(Number(value))) return "-";
        return `${Math.round(Number(value))}ms`;
    }

    function formatDistance(value) {
        if (value == null || Number.isNaN(Number(value))) return "-";
        return `${Number(value).toFixed(1)}m`;
    }

    function formatGcdKind(value) {
        if (value === "Instant" || value === 1) return "瞬发";
        if (value === "Casted" || value === 2) return "读条";
        return "-";
    }

    function formatValue(value) {
        if (value == null) return "-";
        if (typeof value === "boolean") return value ? "是" : "否";
        return String(value);
    }

    function formatTime(timestamp) {
        if (!timestamp) return "-";
        const date = new Date(timestamp);
        if (Number.isNaN(date.getTime())) return timestamp;
        const hh = String(date.getHours()).padStart(2, "0");
        const mm = String(date.getMinutes()).padStart(2, "0");
        const ss = String(date.getSeconds()).padStart(2, "0");
        const ms = String(date.getMilliseconds()).padStart(3, "0");
        return `${hh}:${mm}:${ss}.${ms}`;
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }
})();
