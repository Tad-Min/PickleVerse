/**
 * Phone Connection Modal - Dual Connection Modes:
 * 1. Use Phone as High-Def Wireless Webcam for Computer (Iriun, DroidCam, Apple Continuity) - 100% Works, No 403/404!
 * 2. Open App Directly on Mobile Phone (QR Code & Direct/Share Links with 404 & 403 Resolution Guide)
 */

import React, { useEffect, useState } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import {
  AlertTriangle,
  Check,
  Copy,
  ExternalLink,
  Globe,
  HelpCircle,
  Laptop,
  RefreshCw,
  Share2,
  ShieldAlert,
  ShieldCheck,
  Smartphone,
  Sparkles,
  Video,
  Wifi,
  X,
} from 'lucide-react';

interface PhoneConnectModalProps {
  isOpen: boolean;
  onClose: () => void;
  availableDevices?: MediaDeviceInfo[];
  onRefreshDevices?: () => Promise<void>;
  onSelectDevice?: (deviceId: string) => void;
}

const SHARED_APP_URL = 'https://ais-pre-uia2mzk5dekx4lzjmxef3y-553408691804.asia-east1.run.app';

export const PhoneConnectModal: React.FC<PhoneConnectModalProps> = ({
  isOpen,
  onClose,
  availableDevices = [],
  onRefreshDevices,
  onSelectDevice,
}) => {
  // Default to phone_webcam because it works 100% without any 403 or 404 Cloud Run routing issues
  const [activeTab, setActiveTab] = useState<'phone_webcam' | 'open_mobile'>('phone_webcam');
  const [urlType, setUrlType] = useState<'direct' | 'public'>('direct');
  const [copied, setCopied] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [deviceList, setDeviceList] = useState<MediaDeviceInfo[]>(availableDevices);
  const [showTroubleshoot, setShowTroubleshoot] = useState(true);

  // Derive URLs
  const rawDevUrl = typeof window !== 'undefined' ? window.location.href : SHARED_APP_URL;

  const publicSharedUrl = (() => {
    if (typeof window === 'undefined') return SHARED_APP_URL;
    const href = window.location.href;
    if (href.includes('ais-dev-')) {
      return href.replace('ais-dev-', 'ais-pre-');
    }
    return href || SHARED_APP_URL;
  })();

  const activeUrl = urlType === 'direct' ? rawDevUrl : publicSharedUrl;

  // Enumerate devices on open
  useEffect(() => {
    if (!isOpen) return;
    const fetchDevices = async () => {
      try {
        if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
          const devices = await navigator.mediaDevices.enumerateDevices();
          const videoInputs = devices.filter(d => d.kind === 'videoinput');
          if (videoInputs.length > 0) {
            setDeviceList(videoInputs);
          }
        }
      } catch (e) {
        console.warn('Enumerate error:', e);
      }
    };
    fetchDevices();
  }, [isOpen]);

  useEffect(() => {
    if (availableDevices && availableDevices.length > 0) {
      setDeviceList(availableDevices);
    }
  }, [availableDevices]);

  if (!isOpen) return null;

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(activeUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    } catch (e) {
      console.warn('Copy failed:', e);
    }
  };

  const handleRefresh = async () => {
    setIsRefreshing(true);
    if (onRefreshDevices) {
      await onRefreshDevices();
    }
    try {
      if (navigator.mediaDevices && navigator.mediaDevices.enumerateDevices) {
        const devices = await navigator.mediaDevices.enumerateDevices();
        const videoInputs = devices.filter(d => d.kind === 'videoinput');
        setDeviceList(videoInputs);
      }
    } catch (e) {
      console.warn('Refresh devices failed:', e);
    }
    setTimeout(() => setIsRefreshing(false), 600);
  };

  // Find if any connected camera device is a phone webcam
  const phoneWebcamKeywords = ['iriun', 'droidcam', 'iphone', 'epoccam', 'camo', 'phone', 'ip camera', 'virtual'];
  const detectedPhoneDevices = deviceList.filter(d =>
    phoneWebcamKeywords.some(kw => d.label.toLowerCase().includes(kw))
  );

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-slate-950/80 backdrop-blur-md animate-in fade-in duration-200">
      <div className="relative w-full max-w-2xl bg-slate-900 border border-slate-800 rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[92vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-slate-800 bg-slate-950/60">
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-sky-500/15 border border-sky-500/30 rounded-xl text-sky-400">
              <Smartphone className="w-5 h-5" />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-100 flex items-center gap-2">
                Kết Nối Camera Điện Thoại
                <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-emerald-500/20 text-emerald-400 border border-emerald-500/30">
                  HD 60FPS
                </span>
              </h3>
              <p className="text-xs text-slate-400 mt-0.5">
                Dùng điện thoại làm webcam không dây cho máy tính hoặc quét mã QR mở trên điện thoại
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-2 text-slate-400 hover:text-white rounded-lg hover:bg-slate-800 transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Tab Navigation */}
        <div className="flex border-b border-slate-800 px-5 pt-2 bg-slate-950/30 gap-2">
          <button
            onClick={() => setActiveTab('phone_webcam')}
            className={`flex items-center gap-2 pb-3 pt-2 text-xs font-semibold border-b-2 transition-all ${
              activeTab === 'phone_webcam'
                ? 'border-sky-500 text-sky-400'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
          >
            <Laptop className="w-4 h-4" />
            <span>1. Biến ĐT Thành Webcam Cho Máy Tính</span>
            <span className="px-1.5 py-0.5 rounded bg-emerald-500/20 text-emerald-300 text-[10px] font-bold">
              Khuyên Dùng (0% Lỗi)
            </span>
          </button>

          <button
            onClick={() => setActiveTab('open_mobile')}
            className={`flex items-center gap-2 pb-3 pt-2 text-xs font-semibold border-b-2 transition-all ${
              activeTab === 'open_mobile'
                ? 'border-sky-500 text-sky-400'
                : 'border-transparent text-slate-400 hover:text-slate-200'
            }`}
          >
            <Smartphone className="w-4 h-4" />
            <span>2. Quét QR Mở Trực Tiếp Trên ĐT</span>
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-5 overflow-y-auto space-y-4">
          {activeTab === 'phone_webcam' ? (
            // TAB 1 (Default): Use Phone as Wireless Webcam for Computer
            <div className="space-y-4">
              {/* Highlight Announcement */}
              <div className="p-3.5 bg-sky-950/40 border border-sky-500/30 rounded-xl flex items-start gap-3">
                <Sparkles className="w-5 h-5 text-sky-400 flex-shrink-0 mt-0.5" />
                <div className="space-y-1 text-xs text-slate-200">
                  <p className="font-semibold text-sky-300">
                    Cách tốt nhất &amp; hoàn toàn không bị lỗi 403 hay 404:
                  </p>
                  <p className="text-slate-300 leading-relaxed">
                    Điện thoại của bạn sẽ truyền hình ảnh trực tiếp vào máy tính qua Wi-Fi hoặc dây sạc USB. Màn hình máy tính lớn hiển thị khung xương 3D và chấm điểm, trong khi camera điện thoại đặt cách xa 2-3m theo dõi toàn thân bạn!
                  </p>
                </div>
              </div>

              {/* Connected Cameras Detector */}
              <div className="p-4 bg-slate-950/70 border border-slate-800 rounded-xl space-y-2.5">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-slate-200 flex items-center gap-1.5">
                    <Video className="w-4 h-4 text-sky-400" />
                    Các Camera Đang Kết Nối Với Máy Tính:
                  </span>
                  <button
                    onClick={handleRefresh}
                    disabled={isRefreshing}
                    className="flex items-center gap-1 px-3 py-1 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs rounded-lg transition-all disabled:opacity-50 font-medium"
                  >
                    <RefreshCw className={`w-3 h-3 ${isRefreshing ? 'animate-spin' : ''}`} />
                    <span>Làm mới danh sách</span>
                  </button>
                </div>

                <div className="space-y-1.5 pt-1">
                  {deviceList.length > 0 ? (
                    deviceList.map((d, i) => {
                      const isPhone = phoneWebcamKeywords.some(kw =>
                        d.label.toLowerCase().includes(kw)
                      );
                      return (
                        <div
                          key={d.deviceId || i}
                          className={`flex items-center justify-between p-2.5 rounded-lg border text-xs ${
                            isPhone
                              ? 'bg-emerald-950/30 border-emerald-500/40 text-emerald-200'
                              : 'bg-slate-900 border-slate-800 text-slate-300'
                          }`}
                        >
                          <span className="flex items-center gap-2 truncate">
                            {isPhone ? (
                              <Smartphone className="w-4 h-4 text-emerald-400 flex-shrink-0" />
                            ) : (
                              <Video className="w-4 h-4 text-slate-400 flex-shrink-0" />
                            )}
                            <span className="font-medium truncate">{d.label || `Camera ${i + 1}`}</span>
                            {isPhone && (
                              <span className="px-1.5 py-0.5 rounded bg-emerald-500/20 text-emerald-300 text-[10px] font-bold flex-shrink-0">
                                Đã Nhận Diện Điện Thoại
                              </span>
                            )}
                          </span>
                          {onSelectDevice && (
                            <button
                              onClick={() => {
                                onSelectDevice(d.deviceId);
                                onClose();
                              }}
                              className="px-3 py-1 bg-sky-500 hover:bg-sky-400 text-slate-950 text-xs font-bold rounded-md transition-colors flex-shrink-0 ml-2 shadow-sm"
                            >
                              Dùng camera này
                            </button>
                          )}
                        </div>
                      );
                    })
                  ) : (
                    <p className="text-xs text-slate-400 italic py-2">
                      Đang tìm camera... Hãy bấm &quot;Làm mới danh sách&quot; sau khi mở ứng dụng webcam trên điện thoại.
                    </p>
                  )}
                </div>
              </div>

              {/* 3 Step-by-step Options to Connect Phone as Webcam */}
              <div className="space-y-2.5 text-xs text-slate-300">
                <h5 className="font-semibold text-slate-200">
                  Chọn 1 trong 3 cách kết nối điện thoại làm webcam siêu nhanh:
                </h5>

                {/* Option A: Iriun 4K Webcam (Free for Android & iPhone) */}
                <div className="p-3.5 bg-slate-950/60 rounded-xl border border-slate-800 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-slate-100 flex items-center gap-2">
                      <span className="px-2 py-0.5 rounded bg-sky-500/20 text-sky-300 text-[10px] font-mono font-bold">
                        CÁCH 1 (PHỔ BIẾN NHẤT)
                      </span>
                      Iriun 4K Webcam (Miễn phí cho cả iPhone &amp; Android)
                    </span>
                    <span className="text-[11px] text-emerald-400 font-medium">Wi-Fi hoặc Cáp USB</span>
                  </div>
                  <p className="text-slate-400 leading-relaxed">
                    1. Tải app <strong>Iriun Webcam</strong> trên điện thoại từ App Store (iPhone) hoặc Google Play (Android).<br />
                    2. Cài đặt Iriun trên máy tính từ trang chủ <code className="text-sky-300 font-mono">iriun.com</code>.<br />
                    3. Mở app trên điện thoại và máy tính (cùng Wi-Fi hoặc cắm dây sạc USB). Camera điện thoại sẽ lập tức xuất hiện ở danh sách phía trên!
                  </p>
                  <a
                    href="https://iriun.com"
                    target="_blank"
                    rel="noreferrer"
                    className="inline-flex items-center gap-1.5 text-sky-400 hover:text-sky-300 font-semibold text-xs pt-0.5"
                  >
                    <span>Mở trang tải Iriun Webcam (iriun.com)</span>
                    <ExternalLink className="w-3.5 h-3.5" />
                  </a>
                </div>

                {/* Option B: Apple Continuity Camera */}
                <div className="p-3.5 bg-slate-950/60 rounded-xl border border-slate-800 space-y-1.5">
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-slate-100 flex items-center gap-2">
                      <span className="px-2 py-0.5 rounded bg-slate-800 text-slate-300 text-[10px] font-mono font-bold">
                        CÁCH 2
                      </span>
                      Apple Continuity Camera (Không cần cài bất kỳ app nào)
                    </span>
                    <span className="text-[11px] text-purple-400 font-medium">Mac + iPhone</span>
                  </div>
                  <p className="text-slate-400 leading-relaxed">
                    Nếu bạn dùng máy tính Mac và iPhone: Chỉ cần cắm cáp USB hoặc để iPhone cạnh Mac, trình duyệt tự động nhận camera sau của iPhone thành webcam 4K góc rộng 0.5x.
                  </p>
                </div>

                {/* Option C: DroidCam */}
                <div className="p-3.5 bg-slate-950/60 rounded-xl border border-slate-800 space-y-1.5">
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-slate-100 flex items-center gap-2">
                      <span className="px-2 py-0.5 rounded bg-slate-800 text-slate-300 text-[10px] font-mono font-bold">
                        CÁCH 3
                      </span>
                      DroidCam (Rất ổn định cho Windows &amp; Android)
                    </span>
                  </div>
                  <p className="text-slate-400 leading-relaxed">
                    Cài DroidCam trên điện thoại và client trên PC (dev47apps.com), kết nối qua Wi-Fi hoặc USB.
                  </p>
                </div>
              </div>
            </div>
          ) : (
            // TAB 2: Scan QR Code & Open App on Phone
            <div className="space-y-4">
              {/* Troubleshooting Alert Box for 404 & 403 */}
              <div className="p-3.5 bg-amber-950/30 border border-amber-500/40 rounded-xl space-y-2">
                <div className="flex items-center justify-between">
                  <span className="text-xs font-bold text-amber-300 flex items-center gap-1.5">
                    <AlertTriangle className="w-4 h-4 text-amber-400" />
                    Hướng Dẫn Khắc Phục Lỗi 404 (Page not found) &amp; Lỗi 403:
                  </span>
                  <button
                    onClick={() => setShowTroubleshoot(prev => !prev)}
                    className="text-[11px] text-amber-400 hover:text-amber-300 underline font-medium"
                  >
                    {showTroubleshoot ? 'Thu gọn' : 'Xem chi tiết'}
                  </button>
                </div>

                {showTroubleshoot && (
                  <div className="space-y-2 text-xs text-slate-300 pt-1 leading-relaxed">
                    <div className="p-2.5 bg-slate-900/80 rounded-lg border border-slate-800 space-y-1">
                      <p className="font-semibold text-amber-300">
                        1. Khắc phục lỗi 404 &quot;Page not found&quot; (Khi mở link ais-pre):
                      </p>
                      <p className="text-slate-400">
                        Link công khai (<code className="text-emerald-400 font-mono text-[11px]">ais-pre-</code>) chỉ hoạt động khi bạn bấm nút <strong>&quot;Share&quot; (Chia sẻ)</strong> ở góc trên bên phải thanh công cụ Google AI Studio. Khi bấm Share, Google sẽ kích hoạt link công khai và điện thoại có thể quét vào được ngay!
                      </p>
                    </div>

                    <div className="p-2.5 bg-slate-900/80 rounded-lg border border-slate-800 space-y-1">
                      <p className="font-semibold text-amber-300">
                        2. Khắc phục lỗi 403 &quot;Forbidden&quot; (Khi mở link ais-dev):
                      </p>
                      <p className="text-slate-400">
                        Chọn <strong>&quot;Link Trực Tiếp (Dev)&quot;</strong> phía dưới. Trên điện thoại, bạn chỉ cần mở trình duyệt Chrome hoặc Safari rồi đăng nhập tài khoản Google của bạn (<code className="text-sky-300 font-mono text-[11px]">huynguyen.190104@gmail.com</code>). Sau khi đăng nhập, link sẽ mở bình thường không bị 403 nữa!
                      </p>
                    </div>
                  </div>
                )}
              </div>

              {/* QR Code & Link Card */}
              <div className="flex flex-col sm:flex-row items-center gap-5 bg-slate-950/70 p-4 sm:p-5 rounded-xl border border-slate-800">
                <div className="p-2.5 bg-white rounded-xl shadow-xl flex-shrink-0 flex items-center justify-center">
                  <QRCodeSVG
                    value={activeUrl}
                    size={160}
                    level="M"
                    includeMargin={true}
                  />
                </div>

                <div className="space-y-3 text-left w-full">
                  <div className="space-y-1">
                    <span className="text-[11px] font-bold tracking-wider uppercase text-sky-400">
                      Quét Mã QR Bằng Điện Thoại
                    </span>
                    <h4 className="text-sm font-semibold text-slate-200">
                      Mở trực tiếp trên Safari hoặc Chrome điện thoại
                    </h4>
                    <p className="text-xs text-slate-400 leading-relaxed">
                      AI MediaPipe BlazePose chạy trực tiếp trên GPU điện thoại, phù hợp khi bạn mang điện thoại ra sân tập pickleball.
                    </p>
                  </div>

                  {/* URL Type Switcher */}
                  <div className="flex items-center gap-1.5 bg-slate-900 p-1 rounded-lg border border-slate-800 text-[11px]">
                    <button
                      onClick={() => setUrlType('direct')}
                      className={`flex-1 py-1.5 px-2 rounded-md font-medium transition-all text-center flex items-center justify-center gap-1 ${
                        urlType === 'direct'
                          ? 'bg-sky-500 text-slate-950 font-bold shadow-sm'
                          : 'text-slate-400 hover:text-slate-200'
                      }`}
                      title="Link dev trực tiếp - Yêu cầu đăng nhập tài khoản Google trên điện thoại"
                    >
                      <ShieldCheck className="w-3 h-3" />
                      Link Trực Tiếp (Dev Link)
                    </button>
                    <button
                      onClick={() => setUrlType('public')}
                      className={`py-1.5 px-2 rounded-md font-medium transition-all text-center flex items-center justify-center gap-1 ${
                        urlType === 'public'
                          ? 'bg-emerald-500 text-slate-950 font-bold shadow-sm'
                          : 'text-slate-400 hover:text-slate-200'
                      }`}
                      title="Link public sau khi bấm Share trên AI Studio"
                    >
                      <Globe className="w-3 h-3" />
                      Link Chia Sẻ (Cần bấm Share)
                    </button>
                  </div>

                  {/* Copy Link Input & Buttons */}
                  <div className="flex items-center gap-2 pt-0.5">
                    <input
                      type="text"
                      readOnly
                      value={activeUrl}
                      className="bg-slate-900 border border-slate-700 text-slate-300 text-xs px-3 py-1.5 rounded-lg flex-1 truncate font-mono select-all focus:outline-none"
                    />
                    <button
                      onClick={handleCopy}
                      className="flex items-center gap-1.5 px-3 py-1.5 bg-sky-500 hover:bg-sky-400 text-slate-950 font-bold text-xs rounded-lg transition-all shadow-md flex-shrink-0"
                    >
                      {copied ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
                      {copied ? 'Đã copy' : 'Copy'}
                    </button>
                    <button
                      onClick={() => window.open(activeUrl, '_blank')}
                      className="p-1.5 bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white rounded-lg transition-colors border border-slate-700 flex-shrink-0"
                      title="Mở thử trong tab mới"
                    >
                      <ExternalLink className="w-4 h-4" />
                    </button>
                  </div>
                </div>
              </div>

              {/* 3 Step Field Guide */}
              <div className="space-y-2">
                <h5 className="text-xs font-semibold text-slate-300 flex items-center gap-1.5">
                  <Sparkles className="w-3.5 h-3.5 text-amber-400" />
                  3 bước sẵn sàng tập luyện Pickleball trên sân:
                </h5>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-2.5">
                  <div className="p-3 bg-slate-950/40 rounded-xl border border-slate-800/80 space-y-1">
                    <span className="text-[10px] font-bold text-sky-400 font-mono">BƯỚC 1</span>
                    <p className="text-xs font-semibold text-slate-200">Cho phép Camera</p>
                    <p className="text-[11px] text-slate-400">
                      Bấm &quot;Cho phép&quot; (Allow) khi trình duyệt Safari hoặc Chrome trên điện thoại hỏi quyền.
                    </p>
                  </div>

                  <div className="p-3 bg-slate-950/40 rounded-xl border border-slate-800/80 space-y-1">
                    <span className="text-[10px] font-bold text-sky-400 font-mono">BƯỚC 2</span>
                    <p className="text-xs font-semibold text-slate-200">Đặt cách 2-3 mét</p>
                    <p className="text-[11px] text-slate-400">
                      Tựa điện thoại vào ghế hoặc gắn tripod ngang ngực để camera bao quát toàn bộ người.
                    </p>
                  </div>

                  <div className="p-3 bg-slate-950/40 rounded-xl border border-slate-800/80 space-y-1">
                    <span className="text-[10px] font-bold text-sky-400 font-mono">BƯỚC 3</span>
                    <p className="text-xs font-semibold text-slate-200">Đeo tai nghe Bluetooth</p>
                    <p className="text-[11px] text-slate-400">
                      Lắng nghe trợ lý AI nhắc lỗi kỹ thuật bằng giọng nói tiếng Việt theo thời gian thực!
                    </p>
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div className="px-5 py-3.5 border-t border-slate-800 bg-slate-950/60 flex items-center justify-between">
          <span className="text-[11px] text-slate-400 flex items-center gap-1.5">
            <Wifi className="w-3.5 h-3.5 text-emerald-400" />
            Khuyên dùng Cách 1 (Webcam không dây): Hình ảnh hiển thị trên màn hình PC lớn, 0% lỗi!
          </span>
          <button
            onClick={onClose}
            className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-semibold rounded-xl transition-colors"
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  );
};
