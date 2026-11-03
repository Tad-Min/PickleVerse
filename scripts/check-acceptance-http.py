"""Exercise real HTTP/JWT/SMTP recovery against the isolated Docker acceptance stack."""
from pathlib import Path
import concurrent.futures, json, re, secrets, time, urllib.request, urllib.error

root=Path(__file__).resolve().parents[1]
artifact=root/'artifacts/account-multiplayer-ai-20261103'
state=json.loads((artifact/'acceptance-private/state.json').read_text(encoding='utf-8-sig'))
checks=[]
def check(ok,name):
    if not ok: raise AssertionError(name)
    checks.append(name); print('PASS:',name,flush=True)
def api(path,body=None,token=None,method=None):
    headers={'Content-Type':'application/json'}
    if token: headers['Authorization']='Bearer '+token
    request=urllib.request.Request(state['apiUrl']+'/'+path, data=None if body is None else json.dumps(body,ensure_ascii=False).encode(),headers=headers,method=method or ('POST' if body is not None else 'GET'))
    try:
        with urllib.request.urlopen(request,timeout=30) as response: return response.status,json.load(response)
    except urllib.error.HTTPError as error:
        return error.code,json.load(error)
def otp(email,subject):
    deadline=time.monotonic()+25
    while time.monotonic()<deadline:
        with urllib.request.urlopen(state['mailUrl']+'/api/v1/messages',timeout=10) as response: messages=json.load(response).get('messages',[])
        for item in messages:
            if subject not in item.get('Subject','') or not any(p.get('Address')==email for p in item.get('To',[])): continue
            with urllib.request.urlopen(state['mailUrl']+'/api/v1/message/'+item['ID'],timeout=10) as response: message=json.load(response)
            code=re.search(r'(?<!\d)\d{6}(?!\d)',message.get('Text',''))
            if code: return code[0]
        time.sleep(.2)
    raise AssertionError('SMTP message was not captured')

def main():
    email='qa-'+secrets.token_hex(6)+'@pickleverse.invalid'
    old='OriginalPass!23';new='ReplacementPass!23'
    status,body=api('api/Auth/register',{'email':email,'password':old,'confirmPassword':old})
    check(status==200 and body['data']['success'],'real API registration accepted')
    verification=otp(email,'Mã xác thực')
    check(api('api/Auth/verify-email',{'email':email,'token':verification})[0]==200,'STARTTLS SMTP verification code accepted')
    status,body=api('api/Auth/login',{'email':email,'password':old})
    check(status==200,'verified account signs in through real HTTP')
    session=body['data'];token=session['accessToken'];refresh=session['refreshToken']
    check(api('api/Profiles/me',token=token)[0]==200,'JWT authentication reads the actual database session')
    status,known=api('api/Auth/forgot-password',{'email':email})
    unknown_status,unknown=api('api/Auth/forgot-password',{'email':'unknown-'+secrets.token_hex(6)+'@pickleverse.invalid'})
    check(status==unknown_status==200 and known['data']==unknown['data'],'known and unknown email receive the same recovery response')
    recovery=otp(email,'Đặt lại mật khẩu')
    check(api('api/Auth/verify-email',{'email':email,'token':recovery})[0]==400,'recovery code cannot verify an email through HTTP')
    request={'email':email,'token':recovery,'password':new,'confirmPassword':new}
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        statuses=list(pool.map(lambda _:api('api/Auth/reset-password',request)[0],range(2)))
    check(sorted(statuses)==[200,400],'simultaneous HTTP resets consume the challenge once')
    check(api('api/Auth/reset-password',request)[0]==400,'HTTP recovery code reuse rejected')
    check(api('api/Profiles/me',token=token)[0]==401,'old JWT is rejected by OnTokenValidated after reset')
    check(api('api/Auth/refresh-token',{'refreshToken':refresh})[0]==401,'old refresh token is revoked through HTTP')
    check(api('api/Auth/login',{'email':email,'password':old})[0]==401,'old password no longer signs in')
    status,body=api('api/Auth/login',{'email':email,'password':new})
    check(status==200,'new password signs in after reset')
    token=body['data']['accessToken']
    check(api('api/Profiles/me',{'isMale':False},token,'PUT')[0]==200,'character selection persists through profile API')
    name='Người chơi '+secrets.token_hex(3)
    check(api('api/Profiles/me',{'playerName':name},token,'PUT')[0]==200,'Vietnamese player name saves through profile API')
    status,profile=api('api/Profiles/me',token=token)
    check(status==200 and profile['data']['isMale'] is False and profile['data']['playerName']==name,'server confirms the selected character and exact Vietnamese name')
    check(api('api/Auth/logout',{'refreshToken':body['data']['refreshToken']},token)[0]==200,'real HTTP logout succeeds')
    report={'passed':True,'count':len(checks),'checks':checks,'smtp':'isolated Mailpit STARTTLS; no external email'}
    (artifact/'http-acceptance.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('SUCCESS:',len(checks),'HTTP/JWT/SMTP/profile checks; OTP/passwords omitted from output.')

if __name__ == '__main__':
    main()
