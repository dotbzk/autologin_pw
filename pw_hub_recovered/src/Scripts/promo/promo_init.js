// Initializes the promo page and registers the Promo.Init module.
(function(){
  try{
    if (!window.Promo || !Promo.register) {
      // Preserve the legacy behavior when promo_core.js is unavailable.
      (function(){
        try{
          var qs = (location.search||'').toLowerCase();
          var isActivate = qs.indexOf('do=activate') !== -1;
          if (!isActivate){
            try {
              if (window.jQuery && window.jQuery.fn && window.jQuery.fn.unbind) {
                window.jQuery('.items_container input[type=checkbox]').unbind('click');
              } else if (window.jQuery) {
                window.jQuery('.items_container input[type=checkbox]').off('click');
              } else {
                var inp = document.querySelectorAll('.items_container input[type=checkbox]');
                if (inp && inp.forEach) inp.forEach(function(el){ try{ if (el && el.onclick) el.onclick = null; }catch(_){} });
              }
            } catch(_) {}
          }
        }catch(_){ }
      })();
      return;
    }

    Promo.register('Init', {
      run: function(){
        Promo.ready(function(){
          try{
            var qs = (location.search||'').toLowerCase();
            var isActivate = qs.indexOf('do=activate') !== -1;
            if (!isActivate){
              try {
                if (window.jQuery && window.jQuery.fn && window.jQuery.fn.unbind) {
                  window.jQuery('.items_container input[type=checkbox]').unbind('click');
                } else if (window.jQuery) {
                  window.jQuery('.items_container input[type=checkbox]').off('click');
                } else {
                  var inp = document.querySelectorAll('.items_container input[type=checkbox]');
                  if (inp && inp.forEach) inp.forEach(function(el){ try{ if (el && el.onclick) el.onclick = null; }catch(_){} });
                }
              } catch(_) {}
            }
          }catch(_){ }
        });
      }
    });
  }catch(_){ }
})();
