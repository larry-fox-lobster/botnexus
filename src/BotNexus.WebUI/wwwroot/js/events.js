// BotNexus WebUI — SignalR event handlers
// All events render to their channel's container, even if hidden.

import { debugLog } from './api.js';
import {
    channelManager, getStreamState, getCurrentSessionId, getCurrentAgentId,
    cleanupSessionState, getCurrentChannelType
} from './session-store.js';
import { hubInvoke, setConnectionId } from './hub.js';
import { setStatus, hideConnectionBanner } from './ui.js';
// Circular-import–safe: hoisted function declarations used only at call-time.
import {
    onMessageStart, onContentDelta, onThinkingDelta, onToolStart, onToolEnd,
    onMessageEnd, onError, updateSessionIdDisplay, syncLoadingUiForActiveSession,
    appendSystemMessage, clearChatMessages, clearSubAgentPanel, renderSubAgentPanel
} from './chat.js';
import { loadSessions, setAgentsCache, trackActivity, updateSidebarBadge } from './sidebar.js';

// ── Sub-agent state ─────────────────────────────────────────────────

export const activeSubAgents = new Map();
export function clearActiveSubAgents() { activeSubAgents.clear(); }

// ── Handler registration ────────────────────────────────────────────

export function registerEventHandlers(connection) {

    connection.on('Connected', (data) => {
        setConnectionId(data.connectionId);
        setAgentsCache(data.agents || []);
        setStatus('connected');
        hideConnectionBanner();
        debugLog('lifecycle', 'Connected! connectionId:', data.connectionId);

        hubInvoke('SubscribeAll').then(result => {
            if (result?.sessions) {
                channelManager.subscribe(result.sessions);
                debugLog('lifecycle', `SubscribeAll: ${result.sessions.length} sessions`);
            }
        }).catch(err => {
            debugLog('lifecycle', 'SubscribeAll failed:', err.message);
        });
    });

    connection.on('SessionReset', (data) => {
        const sid = data?.sessionId || channelManager.activeViewId;
        cleanupSessionState(sid);
        if (sid !== channelManager.activeViewId) return;
        channelManager.setActiveView(null, getCurrentAgentId(), getCurrentChannelType());
        updateSessionIdDisplay();
        syncLoadingUiForActiveSession();
        clearSubAgentPanel();
        clearChatMessages();
        appendSystemMessage('Session reset. System prompt regenerated.');
        loadSessions();
    });

    // ── Stream lifecycle ────────────────────────────────────────────

    connection.on('MessageStart', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!ctx) return;
        const ss = ctx.streamState;
        ss.activeMessageId = evt.messageId;
        ss.isStreaming = true;
        ss.activeToolCount = 0;
        ss.thinkingBuffer = '';
        ss.toolCallDepth = 0;
        ss.toolStartTimes = {};
        // Always render to the channel's container
        onMessageStart(ctx, evt, ctx.sessionId);
        if (!isActive) {
            ctx.unreadCount++;
            updateSidebarBadge(ctx.sessionId, ctx.unreadCount);
        }
    });

    connection.on('ContentDelta', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!ctx) return;
        const text = typeof evt === 'string' ? evt : (evt?.contentDelta || evt?.delta || '');
        if (text) {
            onContentDelta(ctx, text);
            if (!isActive) {
                ctx.unreadCount++;
                updateSidebarBadge(ctx.sessionId, ctx.unreadCount);
            }
        }
    });

    connection.on('ThinkingDelta', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!ctx) return;
        const text = evt?.thinkingContent || evt?.delta || '';
        if (text) ctx.streamState.thinkingBuffer += text;
        if (text) onThinkingDelta(ctx, text);
    });

    connection.on('ToolStart', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!ctx) return;
        // Always render tool to the channel's container
        onToolStart(ctx, evt);
    });

    connection.on('ToolEnd', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!ctx) return;
        onToolEnd(ctx, evt);
    });

    connection.on('MessageEnd', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!ctx) return;
        ctx.streamState.isStreaming = false;
        ctx.streamState.activeMessageId = null;
        onMessageEnd(ctx, evt);
        if (!isActive) {
            ctx.unreadCount++;
            updateSidebarBadge(ctx.sessionId, ctx.unreadCount);
        }
    });

    connection.on('Error', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!ctx) return;
        ctx.streamState.isStreaming = false;
        onError(ctx, evt);
        if (!isActive) {
            ctx.unreadCount++;
            updateSidebarBadge(ctx.sessionId, ctx.unreadCount);
        }
    });

    // ── Sub-agent lifecycle ─────────────────────────────────────────

    connection.on('SubAgentSpawned', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!evt?.subAgentId) return;
        activeSubAgents.set(evt.subAgentId, {
            subAgentId: evt.subAgentId,
            name: evt.name || evt.subAgentId,
            task: evt.task || '',
            model: evt.model || '',
            status: 'Running',
            startedAt: evt.startedAt || new Date().toISOString(),
            completedAt: null, turnsUsed: 0, resultSummary: null
        });
        if (!isActive) return;
        renderSubAgentPanel();
        trackActivity('tool', getCurrentAgentId(), `🚀 Sub-agent spawned: ${evt.name || evt.subAgentId}`);
    });

    connection.on('SubAgentCompleted', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!evt?.subAgentId) return;
        const sa = activeSubAgents.get(evt.subAgentId);
        if (sa) {
            sa.status = 'Completed';
            sa.completedAt = evt.completedAt || new Date().toISOString();
            sa.turnsUsed = evt.turnsUsed || sa.turnsUsed;
            sa.resultSummary = evt.resultSummary || null;
        }
        if (!isActive) return;
        renderSubAgentPanel();
        trackActivity('response', getCurrentAgentId(), `✅ Sub-agent completed: ${evt.name || evt.subAgentId}`);
    });

    connection.on('SubAgentFailed', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!evt?.subAgentId) return;
        const sa = activeSubAgents.get(evt.subAgentId);
        if (sa) {
            sa.status = evt.timedOut ? 'TimedOut' : 'Failed';
            sa.completedAt = evt.completedAt || new Date().toISOString();
            sa.resultSummary = evt.error || evt.resultSummary || null;
        }
        if (!isActive) return;
        renderSubAgentPanel();
        const icon = evt.timedOut ? '⏱' : '❌';
        trackActivity('error', getCurrentAgentId(), `${icon} Sub-agent failed: ${evt.name || evt.subAgentId}`);
    });

    connection.on('SubAgentKilled', (evt) => {
        const { ctx, isActive } = channelManager.routeEvent(evt);
        if (!evt?.subAgentId) return;
        const sa = activeSubAgents.get(evt.subAgentId);
        if (sa) {
            sa.status = 'Killed';
            sa.completedAt = evt.completedAt || new Date().toISOString();
        }
        if (!isActive) return;
        renderSubAgentPanel();
        trackActivity('tool', getCurrentAgentId(), `🛑 Sub-agent killed: ${evt.name || evt.subAgentId}`);
    });
}
