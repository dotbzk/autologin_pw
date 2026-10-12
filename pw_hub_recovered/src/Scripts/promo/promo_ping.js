// Sends a one-time diagnostic ping when the Promo.Ping module is injected.
(function(){
  try{
    function doPing(){
      try{
        if (window.chrome && window.chrome.webview && window.chrome.webview.postMessage) {
          window.chrome.webview.postMessage(JSON.stringify({ type: 'promo_ping2', ts: String(Date.now()) }));
        }
      }catch(_){ }
    }

    if (!window.Promo || !Promo.register){
      // Run immediately when the modular API is unavailable.
      doPing();
      return;
    }

    Promo.register('Ping', { run: doPing });
  }catch(_){ }
})();
