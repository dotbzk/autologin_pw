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
  ReportProgressMsg(0, 'Starting: '..tostring(progress.total)..' accounts, '..tostring(win or 1)..' windows')
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
  if not aChange(handle, accountId) then return { status='error', error='could not switch account' } end
  if not ensureAuthorized(handle) then aChange(handle, accountId); if not ensureAuthorized(handle) then return { status='error', error='not authorized (timeout)' } end end
  local url='https://pwonline.ru/pin/'..promoCode
  aNav(handle, url)
  if not aWait(handle, '.pin_code_input', 30000) then return { status='error', error='.pin_code_input was not found (timeout)' } end
  local r1=tostring(aExec(handle, JS_CLICK_PIN_PRIMARY) or '')
  if r1~='pin_clicked' then aExec(handle, JS_CLICK_PIN_FALLBACK) end
  aExec(handle, JS_WAIT_READY)
  await(function(k) DelayCb(700, function() k(true) end) end)
  local is503=tostring(aExec(handle, JS_IS_503) or ''):lower()
  if is503=='true' then return { status='error 503' } end
  if aExists(handle, '.pin_bonuses_list') then return { status='success' } end
  if aExists(handle, '.m_error') then local txt=tostring(aExec(handle, JS_READ_ERROR_TEXT) or '') if txt=='' then txt='unknown error' end return { status='error', error=txt } end
  await(function(k) DelayCb(1000, function() k(true) end) end)
  if aExists(handle, '.pin_bonuses_list') then return { status='success' } end
  if aExists(handle, '.m_error') then local txt2=tostring(aExec(handle, JS_READ_ERROR_TEXT) or '') if txt2=='' then txt2='unknown error' end return { status='error', error=txt2 } end
  return { status='error', error='could not determine the result' }
end

-- Coroutine body for one browser group.
groupMain = function(groupIndex, accountIds)
  local handle = aCreate({})
  aDelay(1000)
  if (not handle) or tonumber(handle)==0 then
    Pfx('[Group #',groupIndex,'] could not create a browser')
    for _,accId in ipairs(accountIds) do results[accId]={ status='error', error='browser was not created' } end
    return
  end
  Pfx('[Group #',groupIndex,'] handle =', handle)
  ReportProgressMsg(progress.last<0 and 0 or progress.last, 'Group #'..tostring(groupIndex)..' started ('..tostring(#accountIds)..' accounts)')
  for i=1,#accountIds do
    -- Recreate the browser after every two accounts to reduce HTTP 503 errors.
    if i > 1 and ((i-1) % 2) == 0 then
      local oldHandle = handle
      Pfx('[Group #',groupIndex,'] account threshold reached; recreating the browser...')
      -- Create the replacement first, then switch over and close the old browser.
      local newHandle = aCreate({})
      aDelay(700)
      if newHandle and tonumber(newHandle) ~= 0 then
        handle = newHandle
        Pfx('[Group #',groupIndex,'] new handle =', handle, '; closing old handle =', oldHandle)
        pcall(aClose, oldHandle)
      else
        Pfx('[Group #',groupIndex,'] browser recreation failed; continuing with the existing window')
      end
    end

    local accId = tostring(accountIds[i])
    local who = nameOf(accId)
    Pfx('[Group #',groupIndex,'] account:', who, '(', i, '/', #accountIds, ')')
    local ok,res = pcall(processAccount, handle, accId, promo)
    if not ok then res = { status='error', error=('lua error: '..tostring(res)) } end
    results[accId] = res or { status='error', error='empty result' }
    -- Report the completed account.
    progress.done = math.min(progress.done + 1, progress.total)
    local msg
    if res and res.status then
      if res.status=='success' then msg = who..': success'
      elseif res.status=='error 503' then msg = who..': error 503'
      elseif res.status=='error' then msg = who..': error — '..(res.error or '')
      else msg = who..': '..tostring(res.status) end
    else
      msg = who..': no result'
    end
    report(msg, false)
    -- Pause briefly between accounts.
    await(function(k) DelayCb(300, function() k(true) end) end)
  end
  Pfx('[Group #',groupIndex,'] finished; closing browser')
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
          if r.status=='success' then okCount=okCount+1
          elseif r.status=='error 503' then e503=e503+1
          elseif r.status=='error' then errCount=errCount+1 end
        end
      end
      -- Always complete at 100 percent.
      progress.done = progress.total
      report('Done: successful='..okCount..', 503='..e503..', errors='..errCount, true)

      Pfx('===== Promo Code Report =====')
      Pfx('Total accounts:', #accounts)
      Pfx('Successful:', okCount)
      Pfx('Error 503:', e503)
      Pfx('Errors:', errCount)
      for _, accId in ipairs(accounts) do
        local r = results[accId]
        local who = nameOf(accId)
        if r then
          if r.status=='success' then Pfx(who, ': success')
          elseif r.status=='error 503' then Pfx(who, ': error 503')
          elseif r.status=='error' then Pfx(who, ': error — ', r.error or '')
          else Pfx(who, ': no result') end
        else
          Pfx(who, ': no result')
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
if #accounts==0 then Pfx('No accounts.'); ReportProgressMsg(0, 'No accounts'); TryFinish(); return end
if promo=='' then Pfx('Promo code is empty.'); ReportProgressMsg(0, 'Empty promo code'); TryFinish(); return end

-- Distribute accounts across exactly N browser groups.
local groups = {}; for i=1,windows do groups[i]={} end
for i=1,#accounts do local g=((i-1)%windows)+1; table.insert(groups[g], accounts[i]) end

-- Initialize progress reporting.
progress.total = #accounts
reportInit(progress.total, windows)

-- Count active groups and start their coroutines.
pending = 0; for gi=1,#groups do if #groups[gi]>0 then pending=pending+1 end end
Pfx('groups:', #groups, 'pending:', pending)
if pending==0 then Pfx('Empty groups.'); ReportProgressMsg(100, 'Done (empty)'); TryFinish(); return end
for gi=1,#groups do local grp = groups[gi]; if #grp>0 then startGroup(gi, grp) end end
