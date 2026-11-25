// The copy button on each command of the landing page's install panels
// (_partials/hm/command.html). Linked from custom/head-end.html.
//
// An enhancement over markup that already works without it: the button
// ships `hidden` and is revealed only once this runs, so a reader without
// JavaScript never meets a control that does nothing. What it copies is
// exactly the command shown; the "$ " prompt is drawn by CSS, so it is not
// in textContent and never lands in the clipboard.
(function () {
  "use strict";

  var DONE_MS = 1500;

  // Where the Clipboard API is unavailable, a page served over plain HTTP
  // say, select the command instead, so a manual copy takes one keystroke.
  function select(text) {
    if (!window.getSelection) return;
    var range = document.createRange();
    range.selectNodeContents(text);
    var selection = window.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
  }

  document.querySelectorAll("[data-hm-copy]").forEach(function (box) {
    var text = box.querySelector(".hm-command__text");
    var button = box.querySelector(".hm-command__copy");
    var status = box.querySelector(".hm-command__status");
    if (!text || !button) return;
    button.hidden = false;
    button.addEventListener("click", function () {
      var done = function () {
        if (status) status.textContent = "Copied";
        button.classList.add("is-done");
        clearTimeout(button._hmDone);
        button._hmDone = setTimeout(function () {
          button.classList.remove("is-done");
          if (status) status.textContent = "";
        }, DONE_MS);
      };
      if (navigator.clipboard && window.isSecureContext) {
        navigator.clipboard.writeText(text.textContent.trim()).then(done, function () { select(text); });
      } else {
        select(text);
      }
    });
  });
})();
