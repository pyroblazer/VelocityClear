import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Settings, Key, Plus, Copy, Trash2, CheckCircle, AlertTriangle } from 'lucide-react';
import { apiKeyApi, type CreateApiKeyResponse } from '../lib/apiKeyApi';

const AVAILABLE_PERMISSIONS = [
  { value: 'transactions:read', label: 'Read Transactions' },
  { value: 'transactions:write', label: 'Create Transactions' },
  { value: 'payments:authorize', label: 'Authorize Payments' },
  { value: 'audit:read', label: 'Read Audit Logs' },
  { value: 'compliance:read', label: 'Read Compliance Data' },
];

export default function SettingsPage() {
  const queryClient = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const [keyName, setKeyName] = useState('');
  const [selectedPerms, setSelectedPerms] = useState<string[]>(['transactions:read', 'transactions:write']);
  const [newKey, setNewKey] = useState<CreateApiKeyResponse | null>(null);
  const [copied, setCopied] = useState(false);
  const [revokeConfirm, setRevokeConfirm] = useState<string | null>(null);

  const { data: keys = [], isLoading } = useQuery({
    queryKey: ['api-keys'],
    queryFn: apiKeyApi.list,
  });

  const createMutation = useMutation({
    mutationFn: () => apiKeyApi.create(keyName, selectedPerms),
    onSuccess: (result) => {
      setNewKey(result);
      setShowCreate(false);
      setKeyName('');
      queryClient.invalidateQueries({ queryKey: ['api-keys'] });
    },
  });

  const revokeMutation = useMutation({
    mutationFn: (id: string) => apiKeyApi.revoke(id),
    onSuccess: () => {
      setRevokeConfirm(null);
      queryClient.invalidateQueries({ queryKey: ['api-keys'] });
    },
  });

  const handleCopy = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const togglePerm = (perm: string) => {
    setSelectedPerms((prev) =>
      prev.includes(perm) ? prev.filter((p) => p !== perm) : [...prev, perm]
    );
  };

  return (
    <div className="min-h-screen bg-[#0A0A0A] text-white p-6">
      <header className="flex items-center gap-3 mb-8 border-b border-[#2A2A2A] pb-4">
        <Settings className="w-6 h-6 text-[#A1A1AA]" />
        <h1 className="text-2xl font-bold">Settings</h1>
      </header>

      {/* API Keys Section */}
      <section className="mb-8">
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-lg font-semibold flex items-center gap-2">
            <Key className="w-5 h-5 text-[#3B82F6]" />
            API Keys
          </h2>
          <button
            onClick={() => { setShowCreate(true); setNewKey(null); }}
            className="flex items-center gap-2 px-4 py-2 bg-[#3B82F6] hover:bg-[#3B82F6]/80 rounded-lg text-sm font-medium transition-colors"
          >
            <Plus size={14} /> Generate New Key
          </button>
        </div>

        {/* New key display */}
        {newKey && (
          <div className="mb-4 bg-[#22C55E]/10 border border-[#22C55E]/30 rounded-lg p-5">
            <div className="flex items-center gap-2 mb-2">
              <CheckCircle size={16} className="text-[#22C55E]" />
              <span className="text-sm font-semibold text-[#22C55E]">API Key Generated</span>
            </div>
            <div className="flex items-center gap-2 mb-3">
              <div className="flex-1 bg-[#0A0A0A] border border-[#2A2A2A] rounded-lg p-3 font-mono text-sm text-[#3B82F6] break-all">
                {newKey.apiKey}
              </div>
              <button
                onClick={() => handleCopy(newKey.apiKey)}
                className="shrink-0 p-2 bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg hover:border-[#3B82F6]/50 transition-colors"
                title="Copy key"
              >
                {copied ? <CheckCircle size={16} className="text-[#22C55E]" /> : <Copy size={16} className="text-[#A1A1AA]" />}
              </button>
            </div>
            <div className="flex items-center gap-2 text-xs text-[#F59E0B]">
              <AlertTriangle size={12} />
              This key will only be shown once. Copy it now.
            </div>
          </div>
        )}

        {/* Create form */}
        {showCreate && (
          <div className="mb-4 bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg p-5">
            <h3 className="text-sm font-semibold mb-3">Generate New API Key</h3>
            <div className="mb-3">
              <label className="block text-xs text-[#A1A1AA] mb-1">Key Name</label>
              <input
                type="text"
                value={keyName}
                onChange={(e) => setKeyName(e.target.value)}
                placeholder="e.g., Production Integration"
                className="w-full bg-[#0A0A0A] border border-[#2A2A2A] rounded-lg p-2 text-sm text-white outline-none focus:border-[#3B82F6]/50"
              />
            </div>
            <div className="mb-4">
              <label className="block text-xs text-[#A1A1AA] mb-2">Permissions</label>
              <div className="flex flex-wrap gap-2">
                {AVAILABLE_PERMISSIONS.map((perm) => (
                  <button
                    key={perm.value}
                    type="button"
                    onClick={() => togglePerm(perm.value)}
                    className={`text-xs px-3 py-1.5 rounded-lg border transition-colors ${
                      selectedPerms.includes(perm.value)
                        ? 'border-[#3B82F6] text-[#3B82F6] bg-[#3B82F6]/10'
                        : 'border-[#2A2A2A] text-[#A1A1AA] bg-[#0A0A0A]'
                    }`}
                  >
                    {perm.label}
                  </button>
                ))}
              </div>
            </div>
            <div className="flex gap-2">
              <button
                onClick={() => createMutation.mutate()}
                disabled={!keyName.trim() || selectedPerms.length === 0 || createMutation.isPending}
                className="px-4 py-2 bg-[#3B82F6] hover:bg-[#3B82F6]/80 disabled:opacity-50 rounded-lg text-sm font-medium transition-colors"
              >
                {createMutation.isPending ? 'Generating...' : 'Generate Key'}
              </button>
              <button
                onClick={() => { setShowCreate(false); setKeyName(''); }}
                className="px-4 py-2 bg-[#0A0A0A] border border-[#2A2A2A] rounded-lg text-sm text-[#A1A1AA] hover:border-[#A1A1AA]/30 transition-colors"
              >
                Cancel
              </button>
            </div>
          </div>
        )}

        {/* Keys table */}
        {isLoading ? (
          <div className="text-center text-[#A1A1AA] py-8">Loading API keys...</div>
        ) : (
          <div className="bg-[#1A1A1A] border border-[#2A2A2A] rounded-lg overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-[#2A2A2A] text-[#A1A1AA]">
                  <th className="text-left p-3 font-medium">Name</th>
                  <th className="text-left p-3 font-medium">Key Prefix</th>
                  <th className="text-left p-3 font-medium">Permissions</th>
                  <th className="text-left p-3 font-medium">Created</th>
                  <th className="text-left p-3 font-medium">Status</th>
                  <th className="text-left p-3 font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {keys.map((key) => (
                  <tr key={key.id} className="border-b border-[#2A2A2A]/50 hover:bg-[#2A2A2A]/30 transition-colors">
                    <td className="p-3 font-medium">{key.name}</td>
                    <td className="p-3 font-mono text-[#3B82F6] text-xs">{key.keyPrefix}...</td>
                    <td className="p-3">
                      <div className="flex gap-1 flex-wrap">
                        {Array.isArray(key.permissions) && key.permissions.map((p) => (
                          <span key={p} className="text-[10px] bg-[#0A0A0A] text-[#A1A1AA] px-2 py-0.5 rounded border border-[#2A2A2A]">{p}</span>
                        ))}
                      </div>
                    </td>
                    <td className="p-3 text-[#A1A1AA]">{new Date(key.createdAt).toLocaleDateString()}</td>
                    <td className="p-3">
                      <span className={`text-xs font-semibold px-2 py-0.5 rounded ${key.isActive ? 'bg-[#22C55E]/20 text-[#22C55E]' : 'bg-[#EF4444]/20 text-[#EF4444]'}`}>
                        {key.isActive ? 'Active' : 'Revoked'}
                      </span>
                    </td>
                    <td className="p-3">
                      {revokeConfirm === key.id ? (
                        <div className="flex items-center gap-2">
                          <span className="text-xs text-[#EF4444]">Confirm?</span>
                          <button onClick={() => revokeMutation.mutate(key.id)} className="text-xs px-2 py-1 bg-[#EF4444] rounded text-white">Yes</button>
                          <button onClick={() => setRevokeConfirm(null)} className="text-xs px-2 py-1 bg-[#2A2A2A] rounded text-[#A1A1AA]">No</button>
                        </div>
                      ) : (
                        <button
                          onClick={() => setRevokeConfirm(key.id)}
                          disabled={!key.isActive}
                          className="text-[#EF4444] hover:text-[#EF4444]/70 disabled:text-[#2A2A2A] transition-colors"
                          title="Revoke key"
                        >
                          <Trash2 size={14} />
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
                {keys.length === 0 && (
                  <tr>
                    <td colSpan={6} className="text-center text-[#A1A1AA] py-8">No API keys yet. Generate one above.</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  );
}
