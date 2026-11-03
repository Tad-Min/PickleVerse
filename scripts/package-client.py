from pathlib import Path
import hashlib, json, zipfile
root=Path(__file__).resolve().parents[1]
output=root/'artifacts/account-multiplayer-ai-20261103'
client=output/'windows-client'
report=json.loads((root/'My project/Temp/client-build.json').read_text(encoding='utf-8-sig'))
if report.get('result')!='Succeeded': raise SystemExit('Build Windows client first.')
for name in ('PickleVerse.exe','UnityPlayer.dll','MonoBleedingEdge','PickleVerse_Data'):
    if not (client/name).exists(): raise SystemExit('Incomplete Windows build: '+name)
readme='''PickleVerse — bản kiểm thử Windows 20261103-p18

Đây là Development Build, dùng protocol 18. Cập nhật backend/schema theo Doc/Implementation_20261103.md trước khi nghiệm thu.
Giải nén toàn bộ thư mục rồi chạy PickleVerse.exe. Hai máy tester dùng cùng gói này.

Ghi chẩn đoán mạng từ PowerShell trong thư mục đã giải nén:
  .\\PickleVerse.exe -pkvNetworkDiagnostics

Tệp network-quality-*.jsonl nằm trong thư mục AppData/LocalLow/DefaultCompany/My project của tài khoản Windows.
Thử đổi người tạo phòng giữa các lượt; ghi video/log của cả hai máy và báo riêng kết quả Editor/build.
Build thành công và unit test không thay thế nghiệm thu gameplay hai thiết bị.
'''
bundle=output/'PickleVerse-Windows-20261103-p18.zip'
with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED,compresslevel=5) as archive:
    for path in sorted(client.rglob('*')):
        if path.is_file() and not path.name.startswith('.unity-pipeline') and not any('BackUpThisFolder' in part for part in path.parts): archive.write(path,str(path.relative_to(client)))
    archive.writestr('HUONG_DAN.txt',readme)
digest=hashlib.sha256(bundle.read_bytes()).hexdigest()
(output/(bundle.name+'.sha256')).write_text(digest+'  '+bundle.name+'\n',encoding='ascii')
(output/'client-build.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'bundle':str(bundle),'bytes':bundle.stat().st_size,'sha256':digest}))
