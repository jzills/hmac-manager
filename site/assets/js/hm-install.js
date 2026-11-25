// The tabs that turn the landing page's install cards into one panel. Each
// command's copy button is assets/js/hm-copy.js.
//
// An enhancement over markup that already works without it: without
// JavaScript the cards stay a grid, every method visible at once.
(function () {
  "use strict";

  // A tab per card, named by the card's own label, so the tab list cannot
  // drift from the methods the cards show.
  function wireTabs(container, n) {
    var cards = Array.prototype.slice.call(
      container.querySelectorAll(".hm-install__card")
    );
    if (cards.length < 2) return;

    var list = document.createElement("div");
    list.className = "hm-tabs";
    list.setAttribute("role", "tablist");
    list.setAttribute("aria-label", container.getAttribute("aria-label") || "Install");

    var tabs = cards.map(function (card, i) {
      var label = card.querySelector(".hm-install__label");
      // The panel is on the page twice, the hero's and the closing one, so
      // the ids carry which panel as well as which card.
      var id = "hm-install-" + n + "-" + i;
      card.id = id + "-panel";
      card.setAttribute("role", "tabpanel");
      card.setAttribute("aria-labelledby", id);

      var tab = document.createElement("button");
      tab.type = "button";
      tab.id = id;
      tab.className = "hm-tabs__tab";
      tab.setAttribute("role", "tab");
      tab.setAttribute("aria-controls", card.id);
      tab.textContent = label ? label.textContent.trim() : "Option " + (i + 1);
      list.appendChild(tab);
      return tab;
    });

    function select(index, focus) {
      tabs.forEach(function (tab, i) {
        var on = i === index;
        tab.setAttribute("aria-selected", on ? "true" : "false");
        tab.tabIndex = on ? 0 : -1;
        cards[i].hidden = !on;
      });
      if (focus) tabs[index].focus();
    }

    tabs.forEach(function (tab, i) {
      tab.addEventListener("click", function () { select(i, false); });
      tab.addEventListener("keydown", function (event) {
        var next = null;
        if (event.key === "ArrowRight") next = (i + 1) % tabs.length;
        if (event.key === "ArrowLeft") next = (i - 1 + tabs.length) % tabs.length;
        if (event.key === "Home") next = 0;
        if (event.key === "End") next = tabs.length - 1;
        if (next !== null) {
          event.preventDefault();
          select(next, true);
        }
      });
    });

    container.classList.add("is-tabbed");
    container.insertBefore(list, container.firstChild);
    select(0, false);
  }

  function init() {
    document.querySelectorAll("[data-hm-tabs]").forEach(function (el, n) { wireTabs(el, n); });
  }

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", init);
  } else {
    init();
  }
})();
