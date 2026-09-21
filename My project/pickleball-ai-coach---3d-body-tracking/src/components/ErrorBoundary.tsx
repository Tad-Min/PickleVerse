import React, { ErrorInfo, ReactNode } from 'react';
import { AlertTriangle, RefreshCw } from 'lucide-react';

interface Props {
  children: ReactNode;
  fallbackTitle?: string;
  fallbackMessage?: string;
  onReset?: () => void;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class ErrorBoundary extends React.Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
    };
  }

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('Uncaught error caught by ErrorBoundary:', error, errorInfo);
  }

  private handleRetry = () => {
    this.setState({ hasError: false, error: null });
    if (this.props.onReset) {
      this.props.onReset();
    }
  };

  public render() {
    if (this.state.hasError) {
      return (
        <div className="w-full h-full min-h-[360px] bg-slate-950/90 rounded-2xl border border-amber-500/30 p-6 flex flex-col items-center justify-center text-center backdrop-blur-md shadow-xl">
          <div className="w-14 h-14 rounded-2xl bg-amber-500/10 border border-amber-500/30 flex items-center justify-center mb-4 text-amber-400">
            <AlertTriangle className="w-7 h-7" />
          </div>
          <h3 className="text-lg font-bold text-slate-100 mb-2">
            {this.props.fallbackTitle || 'Không gian 3D tạm thời gặp sự cố'}
          </h3>
          <p className="text-sm text-slate-400 max-w-md mb-5 leading-relaxed">
            {this.props.fallbackMessage ||
              'Trình duyệt đang giải phóng ngữ cảnh WebGL GPU. Hãy thử bấm khởi động lại để kích hoạt lại mô hình 3D.'}
          </p>
          <button
            onClick={this.handleRetry}
            className="flex items-center gap-2 px-5 py-2.5 bg-gradient-to-r from-sky-500 to-emerald-500 hover:from-sky-400 hover:to-emerald-400 text-white font-semibold text-sm rounded-xl shadow-lg transition-all active:scale-95 cursor-pointer"
          >
            <RefreshCw className="w-4 h-4" />
            Khởi động lại Không Gian 3D
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
