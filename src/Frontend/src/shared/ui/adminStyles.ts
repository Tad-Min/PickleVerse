// Bộ class dùng chung cho các trang quản trị.

export const pageTitle = 'text-2xl font-bold font-display text-foreground tracking-tight';
export const pageSubtitle = 'text-sm text-muted-foreground mt-1';
export const sectionTitle = 'text-lg font-bold font-display text-foreground';

export const toolbar = 'bg-card border border-border rounded-2xl p-4 flex flex-col md:flex-row items-center justify-between gap-4 shadow-card';
export const card = 'bg-card border border-border rounded-2xl p-5 shadow-card';
export const tableCard = 'bg-card border border-border rounded-2xl shadow-card overflow-hidden';
export const tableHead = 'bg-background/70 border-b border-border text-muted-foreground uppercase tracking-wider text-[11px] font-semibold';
export const th = 'py-3.5 px-4 text-left';
export const thRight = 'py-3.5 px-4 text-right';
export const td = 'py-3 px-4';
export const tableText = 'w-full text-left text-xs';
export const rowHover = 'hover:bg-primary-soft/50 transition-colors';

export const selectClass = 'bg-background border border-border rounded-lg text-xs text-foreground px-3 py-2 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-shadow';
export const inputClass = 'w-full px-3 py-2 bg-background border border-border rounded-lg text-xs text-foreground placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary transition-shadow';
export const fieldLabel = 'block space-y-1 text-xs font-medium text-foreground';

export const btnPrimary = 'px-4 py-1.5 text-xs font-semibold bg-primary hover:bg-primary-hover text-white rounded-lg shadow-sm shadow-primary/25 active:scale-[0.98] disabled:opacity-40 disabled:cursor-not-allowed disabled:shadow-none transition-all';
export const btnGhost = 'px-4 py-1.5 text-xs font-semibold bg-card border border-border hover:bg-muted text-foreground rounded-lg active:scale-[0.98] disabled:opacity-40 transition-all';
export const btnSmall = 'px-2.5 py-1 text-[11px] font-semibold bg-card border border-border hover:bg-muted text-foreground rounded-md disabled:opacity-40 transition-colors';
export const btnTab = (active: boolean) =>
  `px-4 py-2 text-xs font-semibold rounded-lg transition-all ${active ? 'bg-primary text-white shadow-sm shadow-primary/30' : 'text-muted-foreground hover:bg-muted hover:text-foreground'}`;

// Badge có chấm trạng thái ở đầu.
export const badgeBase = "inline-flex items-center gap-1.5 whitespace-nowrap px-2.5 py-0.5 rounded-full text-[11px] font-medium border before:content-[''] before:h-1.5 before:w-1.5 before:rounded-full before:bg-current";
export const tone = {
  emerald: 'bg-emerald-500/10 text-emerald-600 border-emerald-500/20',
  rose: 'bg-rose-500/10 text-rose-600 border-rose-500/20',
  amber: 'bg-amber-500/10 text-amber-600 border-amber-500/20',
  indigo: 'bg-indigo-500/10 text-indigo-600 border-indigo-500/20',
  blue: 'bg-blue-500/10 text-blue-600 border-blue-500/20',
  neutral: 'bg-muted text-muted-foreground border-border',
} as const;

export const notice = (type: 'success' | 'error') =>
  `p-3.5 rounded-xl flex items-center justify-between text-xs font-medium border ${type === 'success' ? tone.emerald : tone.rose}`;

export const modalOverlay = 'fixed inset-0 !m-0 z-50 bg-slate-900/30 backdrop-blur-[2px] flex items-center justify-center p-4 animate-fade-in';
export const modalCard = 'bg-card border border-border rounded-2xl shadow-lift p-6 space-y-4 w-full max-w-xl max-h-[90vh] overflow-auto animate-fade-up';
