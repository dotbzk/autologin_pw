-- =============================================================
-- FORWARD DECLARATIONS
-- =============================================================
local Pfx
local safeLen, toAccountId, normalizeAccounts
local await, aCreate, aClose, aNav, aWait, aExists, aExec, aChange
local ensureAuthorized, processAccount, groupMain, startGroup
-- Account names
local nameById, seedNamesFromArgs, nameOf
-- Progress reporting
local progress, reportInit, report
-- Completion
local _reported, TryFinish

-- Input values supplied through args.
local accounts_raw = (args and args.Accounts) or {}
local windows = tonumber(args and args.Windows or 1) or 1
if windows < 1 then windows = 1 end
local promo = tostring(args and args.PromoCode or '')

-- JavaScript helpers.
local JS_IS_503, JS_WAIT_READY, JS_CLICK_PIN_PRIMARY, JS_READ_ERROR_TEXT

-- Shared results and active-group counter.
local results = {}
local pending = 0

-- =============================================================
-- IMPLEMENTATION
-- =============================================================
Pfx = function(...) local b={} for i=1,select('#', ...) do b[i]=tostring(select(i,...)) end Print(table.concat(b, ' ')) end

-- JavaScript utilities.
JS_IS_503 = [[(function(){try{var t=(document.title||'')+' '+(document.body&&document.body.innerText||'');t=t.toLowerCase();return t.indexOf('503')>=0;}catch(e){}return false;})()]]
JS_WAIT_READY = [[(function(){return document.readyState;})()]]
JS_CLICK_PIN_PRIMARY = [[(function(){try{var b=document.querySelector("button[type='submit'], input[type='submit']");if(b){b.click();return 'submit_clicked';}}catch(e){}return 'submit_not_found';})()]]
JS_READ_ERROR_TEXT = [[(function(){try{var el=document.querySelector('.m_error');return el?(el.innerText||''):'';}catch(e){return '';} })()]]

-- Safely determine the highest numeric table index.
safeLen = function(t) if type(t)~='table' then return 0 end local n=0 for k,_ in pairs(t) do if type(k)=='number' and k>0 and k%1==0 and k>n then n=k end end return n end

-- Account-name lookup.
nameById = {}
seedNamesFromArgs = function(list)
  if type(list)~='table' then return end
  for _,v in pairs(list) do
    if type(v)=='table' then
      local id = (type(v.Id)=='string' and v.Id) or nil
      local nm = (v.Name and tostring(v.Name)) or ''
      if id and id~='' and nm~='' then nameById[id]=nm end
    end
  end
end
nameOf = function(id) local nm=nameById[id]; return (nm and nm~='' and nm) or id end

-- Normalize account values into an array of IDs.
toAccountId = function(x) if type(x)=='string' then return x end if type(x)=='table' and x.Id~=nil then return tostring(x.Id) end return nil end
normalizeAccounts = function(list) local out,i={},1 if type(list)~='table' then return out end for _,v in pairs(list) do local id=toAccountId(v); if id and id~='' then out[i]=id; i=i+1 end end return out end

-- Progress reporting.
progress = { total = 0, done = 0, last = -1 }
reportInit = function(total, win)
  progress.total = total or 0
  progress.done = 0
  progress.last = -1
  ReportProgressMsg(0, 'Старт: аккаунтов '..tostring(progress.total)..', окон '..tostring(win or 1))
end
report = function(message, force)
  local pct = 0
  if progress.total > 0 then pct = math.floor((progress.done / progress.total) * 100) end
  if force or pct ~= progress.last then
    ReportProgressMsg(pct, message or '')
    progress.last = pct
  else
    -- Uncomment to report every message even when the percentage is unchanged.
    -- ReportProgressMsg(pct, message or '')
  end
end

-- Coroutine-based await wrappers.
await = function(starter) local co=coroutine.running(); if not co then error('await must be used inside a coroutine') end local resumed=false starter(function(...) if not resumed then resumed=true; local ok,err=coroutine.resume(co, ...); if not ok then Pfx('resume error:', tostring(err)) end end end) return coroutine.yield() end
aCreate=function(options) return await(function(k) BrowserV2_Create(options or {}, function(h) k(h) end) end) end
aClose=function(handle) return await(function(k) BrowserV2_Close(handle, function(ok) k(ok==true) end) end) end
aNav=function(handle,url) return await(function(k) BrowserV2_Navigate(handle,url,function(ok) k(ok==true) end) end) end
aWait=function(handle,sel,ms) return await(function(k) BrowserV2_WaitForElement(handle,sel,ms or 5000,function(f) k(f==true) end) end) end
aExists=function(handle,sel) return await(function(k) BrowserV2_ElementExists(handle,sel,function(e) k(e==true) end) end) end
aExec=function(handle,js) return await(function(k) BrowserV2_ExecuteScript(handle,js,function(res) k(res) end) end) end
aChange=function(handle,accId) return await(function(k) BrowserV2_ChangeAccount(handle,accId,function(ok) k(ok==true) end) end) end
aDelay=function(timeMs) return await(function(k) DelayCb(timeMs,function() k(true) end) end) end

-- Complete the operation exactly once.
_reported = false
TryFinish = function() if _reported then return end _reported = true DelayCb(0, function() Complete('ok') end) end

-- Check whether the browser session is authorized.
ensureAuthorized = function(handle) aNav(handle,'https://pwonline.ru/'); return aWait(handle,'.main_menu',30000) end

-- Apply the promo code to one account.
processAccount = function(handle, accountId, promoCode)
  if not aChange(handle, accountId) then return { status='ошибка', error='не удалось сменить аккаунт' } end
  if not ensureAuthorized(handle) then aChange(handle, accountId); if not ensureAuthorized(handle) then return { status='ошибка', error='не авторизован (таймаут)' } end end
  local url='https://pwonline.ru/pin/'..promoCode
  aNav(handle, url)
  if not aWait(handle, '.pin_code_input', 30000) then return { status='ошибка', error='кнопка .pin_code_input не найдена (таймаут)' } end
  local r1=tostring(aExec(handle, JS_CLICK_PIN_PRIMARY) or '')
  if r1~='pin_clicked' then aExec(handle, JS_CLICK_PIN_FALLBACK) end
  aExec(handle, JS_WAIT_READY)
  await(function(k) DelayCb(700, function() k(true) end) end)
  local is503=tostring(aExec(handle, JS_IS_503) or ''):lower()
  if is503=='true' then return { status='ошибка 503' } end
  if aExists(handle, '.pin_bonuses_list') then return { status='успешно' } end
  if aExists(handle, '.m_error') then local txt=tostring(aExec(handle, JS_READ_ERROR_TEXT) or '') if txt=='' then txt='неизвестная ошибка' end return { status='ошибка', error=txt } end
  await(function(k) DelayCb(1000, function() k(true) end) end)
  if aExists(handle, '.pin_bonuses_list') then return { status='успешно' } end
  if aExists(handle, '.m_error') then local txt2=tostring(aExec(handle, JS_READ_ERROR_TEXT) or '') if txt2=='' then txt2='неизвестная ошибка' end return { status='ошибка', error=txt2 } end
  return { status='ошибка', error='не удалось определить результат' }
end

-- Coroutine body for one browser group.
groupMain = function(groupIndex, accountIds)
  local handle = aCreate({})
  aDelay(1000)
  if (not handle) or tonumber(handle)==0 then
    Pfx('[Группа #',groupIndex,'] не удалось создать браузер')
    for _,accId in ipairs(accountIds) do results[accId]={ status='ошибка', error='браузер не создан' } end
    return
  end
  Pfx('[Группа #',groupIndex,'] handle =', handle)
  ReportProgressMsg(progress.last<0 and 0 or progress.last, 'Группа #'..tostring(groupIndex)..' стартовала ('..tostring(#accountIds)..' акк.)')
  for i=1,#accountIds do
    -- Recreate the browser after every two accounts to reduce HTTP 503 errors.
    if i > 1 and ((i-1) % 2) == 0 then
      local oldHandle = handle
      Pfx('[Группа #',groupIndex,'] достигнут порог 5 аккаунтов, пересоздаю браузер...')
      -- Create the replacement first, then switch over and close the old browser.
      local newHandle = aCreate({})
      aDelay(700)
      if newHandle and tonumber(newHandle) ~= 0 then
        handle = newHandle
        Pfx('[Группа #',groupIndex,'] новый handle =', handle, '; закрываю старый =', oldHandle)
        pcall(aClose, oldHandle)
      else
        Pfx('[Группа #',groupIndex,'] не удалось пересоздать браузер — продолжаю со старым окном')
      end
    end

    local accId = tostring(accountIds[i])
    local who = nameOf(accId)
    Pfx('[Группа #',groupIndex,'] аккаунт:', who, '(', i, '/', #accountIds, ')')
    local ok,res = pcall(processAccount, handle, accId, promo)
    if not ok then res = { status='ошибка', error=('lua error: '..tostring(res)) } end
    results[accId] = res or { status='ошибка', error='пустой результат' }
    -- Report the completed account.
    progress.done = math.min(progress.done + 1, progress.total)
    local msg
    if res and res.status then
      if res.status=='успешно' then msg = who..': успешно'
      elseif res.status=='ошибка 503' then msg = who..': ошибка 503'
      elseif res.status=='ошибка' then msg = who..': ошибка — '..(res.error or '')
      else msg = who..': '..tostring(res.status) end
    else
      msg = who..': нет результата'
    end
    report(msg, false)
    -- Pause briefly between accounts.
    await(function(k) DelayCb(300, function() k(true) end) end)
  end
  Pfx('[Группа #',groupIndex,'] завершилась, закрываю браузер')
  aClose(handle)
end

-- Start one group coroutine and finalize the report when all groups finish.
startGroup = function(groupIndex, accountIds)
  local co = coroutine.create(function()
    groupMain(groupIndex, accountIds)
    -- This group has finished.
    pending = pending - 1
    if pending <= 0 then
      -- Print the final report and finish progress at 100 percent.
      local accounts = normalizeAccounts(accounts_raw)
      local okCount,e503,errCount = 0,0,0
      for _, accId in ipairs(accounts) do
        local r = results[accId]
        if r then
          if r.status=='успешно' then okCount=okCount+1
          elseif r.status=='ошибка 503' then e503=e503+1
          elseif r.status=='ошибка' then errCount=errCount+1 end
        end
      end
      -- Always complete at 100 percent.
      progress.done = progress.total
      report('Готово: успешно='..okCount..', 503='..e503..', ошибок='..errCount, true)

      Pfx('===== Отчёт по вводу промокода =====')
      Pfx('Всего аккаунтов:', #accounts)
      Pfx('Успешно:', okCount)
      Pfx('Ошибка 503:', e503)
      Pfx('Ошибок:', errCount)
      for _, accId in ipairs(accounts) do
        local r = results[accId]
        local who = nameOf(accId)
        if r then
          if r.status=='успешно' then Pfx(who, ': успешно')
          elseif r.status=='ошибка 503' then Pfx(who, ': ошибка 503')
          elseif r.status=='ошибка' then Pfx(who, ': ошибка — ', r.error or '')
          else Pfx(who, ': нет результата') end
        else
          Pfx(who, ': нет результата')
        end
      end
      TryFinish()
    end
  end)
  local ok,err = coroutine.resume(co); if not ok then Pfx('Coroutine start error:', tostring(err)) end
end

-- ===============================
-- MAIN EXECUTION FLOW
-- ===============================
seedNamesFromArgs(accounts_raw)
local accounts = normalizeAccounts(accounts_raw)
Pfx('accounts normalized:', #accounts)
Pfx('windows:', windows)
Pfx('promo:', promo)
if #accounts==0 then Pfx('Нет аккаунтов.'); ReportProgressMsg(0, 'Нет аккаунтов'); TryFinish(); return end
if promo=='' then Pfx('Промокод пуст.'); ReportProgressMsg(0, 'Пустой промокод'); TryFinish(); return end

-- Distribute accounts across exactly N browser groups.
local groups = {}; for i=1,windows do groups[i]={} end
for i=1,#accounts do local g=((i-1)%windows)+1; table.insert(groups[g], accounts[i]) end

-- Initialize progress reporting.
progress.total = #accounts
reportInit(progress.total, windows)

-- Count active groups and start their coroutines.
pending = 0; for gi=1,#groups do if #groups[gi]>0 then pending=pending+1 end end
Pfx('groups:', #groups, 'pending:', pending)
if pending==0 then Pfx('Пустые группы.'); ReportProgressMsg(100, 'Готово (пусто)'); TryFinish(); return end
for gi=1,#groups do local grp = groups[gi]; if #grp>0 then startGroup(gi, grp) end end
