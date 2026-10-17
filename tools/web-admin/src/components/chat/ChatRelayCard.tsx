import React, { useState, useEffect, useCallback, useRef } from 'react';
import { api } from '../../lib/api';
import { ChatHistoryEntry } from '../../types/chat';
import { 
  MessageSquare, 
  Send, 
  RefreshCw, 
  Gamepad2, 
  Globe, 
  Clock, 
  AlertCircle 
} from 'lucide-react';

export const ChatRelayCard: React.FC = () => {
  const [messages, setMessages] = useState<ChatHistoryEntry[]>([]);
  const [inputText, setInputText] = useState('');
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const fetchHistory = useCallback(async () => {
    try {
      const list = await api.chat.getHistory();
      setMessages(list || []);
    } catch {
      // Background poll silently fails if offline
    }
  }, []);

  useEffect(() => {
    fetchHistory();
    const interval = setInterval(fetchHistory, 3000);
    return () => clearInterval(interval);
  }, [fetchHistory]);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleSendMessage = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    const trimmed = inputText.trim();
    if (!trimmed || isSending) return;

    setIsSending(true);
    setError(null);
    try {
      await api.chat.sendMessage(trimmed);
      setInputText('');
      await fetchHistory();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Gagal mengirim pesan chat';
      setError(msg);
    } finally {
      setIsSending(false);
    }
  };

  const formatTime = (isoString: string) => {
    try {
      const d = new Date(isoString);
      return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    } catch {
      return '';
    }
  };

  return (
    <div className="bg-white border border-slate-200 rounded-xl shadow-xs overflow-hidden flex flex-col h-[560px]">
      {/* Header */}
      <div className="px-6 py-4 border-b border-slate-200 flex items-center justify-between bg-slate-50/50">
        <div className="flex items-center space-x-3">
          <div className="p-2 bg-indigo-600 text-white rounded-lg">
            <MessageSquare className="w-5 h-5" />
          </div>
          <div>
            <h2 className="text-base font-semibold text-slate-900 tracking-tight">
              Relay Obrolan Langsung (Web &lt;-&gt; In-Game Chat)
            </h2>
            <p className="text-xs text-slate-500">
              Kirim dan pantau percakapan pemain langsung dengan server secara real-time
            </p>
          </div>
        </div>

        <button
          type="button"
          onClick={fetchHistory}
          className="inline-flex items-center space-x-1.5 px-3 py-1.5 bg-white hover:bg-slate-50 border border-slate-200 rounded-lg text-xs font-medium text-slate-700 transition-colors cursor-pointer"
        >
          <RefreshCw className="w-3.5 h-3.5" />
          <span>Segarkan</span>
        </button>
      </div>

      {error && (
        <div className="px-6 py-2.5 bg-rose-50 border-b border-rose-200 text-rose-800 text-xs flex items-center space-x-2">
          <AlertCircle className="w-4 h-4 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Messages Scroll Area */}
      <div className="flex-1 overflow-y-auto p-6 space-y-3 bg-slate-50/40">
        {messages.length === 0 ? (
          <div className="h-full flex flex-col items-center justify-center text-slate-400 text-xs">
            <MessageSquare className="w-8 h-8 mb-2 text-slate-300" />
            <p className="font-medium text-slate-600">Belum Ada Riwayat Obrolan</p>
            <p className="text-slate-400 mt-0.5">
              Pesan dari pemain di dalam game atau dari form di bawah akan muncul di sini.
            </p>
          </div>
        ) : (
          messages.map((m) => {
            const isWeb = m.source === 'web';
            return (
              <div
                key={m.id}
                className={`flex flex-col max-w-[80%] rounded-xl p-3 text-xs shadow-2xs border ${
                  isWeb
                    ? 'ml-auto bg-indigo-50 border-indigo-200 text-slate-800'
                    : 'mr-auto bg-white border-slate-200 text-slate-800'
                }`}
              >
                <div className="flex items-center justify-between space-x-2 mb-1">
                  <div className="flex items-center space-x-1.5">
                    {isWeb ? (
                      <span className="inline-flex items-center space-x-1 px-1.5 py-0.5 rounded text-[10px] font-semibold bg-indigo-100 text-indigo-700">
                        <Globe className="w-3 h-3" />
                        <span>Web</span>
                      </span>
                    ) : (
                      <span className="inline-flex items-center space-x-1 px-1.5 py-0.5 rounded text-[10px] font-semibold bg-emerald-100 text-emerald-700">
                        <Gamepad2 className="w-3 h-3" />
                        <span>In-Game</span>
                      </span>
                    )}
                    <span className="font-bold text-slate-900">{m.author}</span>
                  </div>
                  <span className="text-[10px] text-slate-400 flex items-center space-x-0.5">
                    <Clock className="w-2.5 h-2.5" />
                    <span>{formatTime(m.timestampUtc)}</span>
                  </span>
                </div>
                <p className="text-slate-700 whitespace-pre-wrap leading-relaxed">{m.message}</p>
              </div>
            );
          })
        )}
        <div ref={messagesEndRef} />
      </div>

      {/* Input Bar */}
      <form
        onSubmit={handleSendMessage}
        className="p-4 bg-white border-t border-slate-200 flex items-center space-x-2"
      >
        <input
          type="text"
          value={inputText}
          onChange={(e) => setInputText(e.target.value)}
          placeholder="Ketik pesan untuk dikirim ke chat in-game..."
          className="flex-1 px-3.5 py-2 text-xs bg-slate-50 border border-slate-200 rounded-lg focus:outline-none focus:ring-1 focus:ring-indigo-500 focus:bg-white transition-all text-slate-900"
          disabled={isSending}
        />
        <button
          type="submit"
          disabled={isSending || !inputText.trim()}
          className="inline-flex items-center space-x-1.5 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold transition-colors disabled:opacity-50 cursor-pointer shadow-xs"
        >
          {isSending ? (
            <RefreshCw className="w-3.5 h-3.5 animate-spin" />
          ) : (
            <Send className="w-3.5 h-3.5" />
          )}
          <span>Kirim</span>
        </button>
      </form>
    </div>
  );
};
