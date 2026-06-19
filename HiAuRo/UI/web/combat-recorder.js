(function () {
    const state = {
        file: null,
        fileName: "",
        warnings: [],
        rows: [],
        groups: [],
        selectedGroupId: null,
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
        fileName: document.getElementById("fileName"),
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
        refs.btnChoose.addEventListener("click", () => refs.fileInput.click());
        refs.btnReload.addEventListener("click", async () => {
            if (!state.file) return;
            await loadFile(state.file);
        });

        refs.fileInput.addEventListener("change", async (event) => {
            const file = event.target.files && event.target.files[0];
            if (!file) return;
            await loadFile(file);
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

    async function loadFile(file) {
        state.file = file;
        state.fileName = file.name;
        const text = await file.text();
        const parsed = parseJsonl(text);
        state.warnings = parsed.warnings;
        state.rows = parsed.rows;
        state.groups = buildGroups(parsed.rows);
        state.selectedGroupId = state.groups.length ? state.groups[0].eventGroupId : null;
        refs.btnReload.disabled = false;
        ensureSelectedGroupVisible();
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
                    current: null
                };
                groupMap.set(groupId, group);
            }

            if (row.sampleRole === "prev" && !group.prev) {
                group.prev = row;
            } else if (row.sampleRole === "current" && !group.current) {
                group.current = row;
            } else if (!group.current) {
                group.current = row;
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
        hasAnyDiff = pushScalar("读条", prev?.isCasting, current?.isCasting, formatBoolean) || hasAnyDiff;
        hasAnyDiff = pushScalar("移动", prev?.isMoving, current?.isMoving, formatBoolean) || hasAnyDiff;
        hasAnyDiff = pushScalar("连击ID", prev?.lastComboSpellId, current?.lastComboSpellId, formatNumber) || hasAnyDiff;
        hasAnyDiff = pushScalar("目标HP%", prev?.targetHpPercent, current?.targetHpPercent, formatPercent) || hasAnyDiff;
        hasAnyDiff = pushScalar("距离", prev?.distance, current?.distance, formatDistance) || hasAnyDiff;

        const gaugeKeys = uniqueKeys(prev?.jobGauge, current?.jobGauge);
        gaugeKeys.forEach((key) => {
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
            if ((before.stack || 0) !== (after.stack || 0)) {
                changes.push(`层数 ${before.stack || 0} -> ${after.stack || 0}`);
            }
            if ((before.remainMs || 0) !== (after.remainMs || 0)) {
                changes.push(`剩余 ${formatMilliseconds(before.remainMs)} -> ${formatMilliseconds(after.remainMs)}`);
            }
            if (changes.length) {
                changed.push(`${after.name} (${after.id}) | ${changes.join(" | ")}`);
            }
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

        if ((prev?.mp ?? null) !== (current?.mp ?? null)) {
            parts.push(`MP ${formatNumber(prev?.mp)} -> ${formatNumber(current?.mp)}`);
        }

        const gaugePriority = ["astralFireStacks", "umbralIceStacks", "umbralHearts", "astralSoulStacks", "polyglotStacks", "isParadoxActive"];
        for (const key of gaugePriority) {
            const before = formatValue(prev?.jobGauge?.[key]);
            const after = formatValue(current?.jobGauge?.[key]);
            if (before !== after) {
                parts.push(`${key} ${before} -> ${after}`);
                break;
            }
        }

        const selfBuffChanges = diff.selfBuffDiff.added.length + diff.selfBuffDiff.removed.length + diff.selfBuffDiff.changed.length;
        if (selfBuffChanges > 0) {
            parts.push(`selfBuff ${selfBuffChanges}项变化`);
        }

        const targetBuffChanges = diff.targetBuffDiff.added.length + diff.targetBuffDiff.removed.length + diff.targetBuffDiff.changed.length;
        if (targetBuffChanges > 0) {
            parts.push(`targetBuff ${targetBuffChanges}项变化`);
        }

        if ((prev?.targetHpPercent ?? null) !== (current?.targetHpPercent ?? null)) {
            parts.push(`target ${formatPercent(prev?.targetHpPercent)} -> ${formatPercent(current?.targetHpPercent)}`);
        }

        if (!parts.length) {
            parts.push(diff.hasAnyDiff ? "存在差异" : "无关键差异");
        }

        return parts.join(" | ");
    }

    function getFilteredGroups() {
        const search = state.filters.search.toLowerCase();
        return state.groups.filter((group) => {
            if (state.filters.eventType !== "all" && group.eventType !== state.filters.eventType) {
                return false;
            }

            if (state.filters.changedOnly && !group.hasDiff) {
                return false;
            }

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

        if (!groups.some(group => group.eventGroupId === state.selectedGroupId)) {
            state.selectedGroupId = groups[0].eventGroupId;
        }
    }

    function render() {
        renderStats();
        renderTimeline();
        renderDetails();
        refs.fileName.textContent = state.fileName || "未选择文件";
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
                  <div class="empty-copy">${state.fileName ? "调整筛选条件，或关闭“只看有变化项”后再试。" : "页面会按 eventGroupId 聚合，并展示每次技能的 prev/current 差异。"}</div>
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
        const badgeClass = group.eventType === "AbilityEffect" ? "ability" : "gcd";
        const badgeText = group.eventType === "AbilityEffect" ? "Ability" : "GCD";
        const selectedClass = group.eventGroupId === state.selectedGroupId ? "selected" : "";
        return `
            <button class="event-row ${selectedClass}" data-group-id="${escapeHtml(group.eventGroupId)}">
              <div class="event-head">
                <div class="event-title">
                  <span class="event-badge ${badgeClass}">${badgeText}</span>
                  <span class="event-name">${escapeHtml(group.actionName || `Action ${group.actionId}`)}</span>
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

        refs.detailsSubtitle.textContent = `${group.actionName} · ${group.eventType}`;
        const warningBlock = state.warnings.length
            ? `<div class="warning-banner">解析时跳过 ${state.warnings.length} 行异常记录。</div>`
            : "";

        refs.detailsContent.innerHTML = `
            ${warningBlock}
            ${renderOverview(group)}
            ${renderScalarSection(group)}
            ${renderGaugeSection(group)}
            ${renderBuffSection("自身 Buff 变化", group.diff.selfBuffDiff)}
            ${renderBuffSection("目标 Buff 变化", group.diff.targetBuffDiff)}
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
              ${renderOverviewCard("来源", snapshot.source || "unknown")}
              ${renderOverviewCard("时间", formatTime(group.timestamp))}
              ${renderOverviewCard("事件组", group.eventGroupId)}
            </div>
        `;
    }

    function renderScalarSection(group) {
        const rows = group.diff.scalarRows.map((row) => `
            <tr class="${row.changed ? "changed" : ""}">
              <th>${escapeHtml(row.label)}</th>
              <td class="${row.changed ? "" : "value-muted"}">${escapeHtml(row.before)}</td>
              <td class="${row.changed ? "delta-text" : "value-muted"}">${escapeHtml(row.after)}</td>
            </tr>
        `).join("");

        return `
            <section class="detail-section">
              <div class="detail-heading">资源与战斗状态</div>
              <div class="detail-body">
                <table class="diff-table">
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
              <th>${escapeHtml(row.label)}</th>
              <td class="${row.changed ? "" : "value-muted"}">${escapeHtml(row.before)}</td>
              <td class="${row.changed ? "delta-text" : "value-muted"}">${escapeHtml(row.after)}</td>
            </tr>
        `).join("");

        return `
            <section class="detail-section">
              <div class="detail-heading">职业量谱</div>
              <div class="detail-body">
                <table class="diff-table">
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
              <div class="detail-body two-col">
                ${renderTagColumn("新增", diff.added, "added")}
                ${renderTagColumn("移除", diff.removed, "removed")}
                ${renderTagColumn("变化", diff.changed, "changed")}
              </div>
            </section>
        `;
    }

    function renderRawSection(group) {
        if (!state.filters.showRawJson) {
            return "";
        }

        return `
            <details class="detail-section">
              <summary>原始 JSON</summary>
              <div class="detail-body two-col">
                <div>
                  <div class="detail-heading">prev</div>
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

    function renderTagColumn(label, items, className) {
        const content = items.length
            ? `<div class="tag-list">${items.map(item => `<span class="tag ${className}">${escapeHtml(item)}</span>`).join("")}</div>`
            : `<div class="value-muted">无</div>`;
        return `
            <div>
              <div class="overview-label">${escapeHtml(label)}</div>
              ${content}
            </div>
        `;
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
