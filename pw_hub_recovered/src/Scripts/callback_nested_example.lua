-- Example: invoke another function from inside a callback.
-- Navigate to a page, read its title through JavaScript, and reload it.

Print('Starting nested-callback example...')

Browser_NavigateCb('https://pwonline.ru/promo_items.php', function(navigateOk)
  if not navigateOk then
    Print('Navigation failed')
    return
  end
  Print('Navigation requested, executing JS...')

  -- Invoke another callback-based function from this callback.
  Browser_ExecuteScriptCb('document.title', function(title)
    Print('document.title = ' .. tostring(title))

    -- Make one more nested API call.
    Browser_ReloadCb(function(reloaded)
      Print('Reload requested: ' .. tostring(reloaded))
      Print('Nested-callback example finished')
    end)
  end)
end)

-- Alternative account example:
-- Account_GetAccountsCb(function(accounts)
--   Print('Accounts in DB: ' .. tostring(#accounts))
--   -- Invoke another API function from inside the callback.
--   Account_GetAccountCb(function(currentName)
--     Print('Current account: ' .. tostring(currentName))
--   end)
-- end)
