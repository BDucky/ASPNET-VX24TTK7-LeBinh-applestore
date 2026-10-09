// Site-wide visual effects: smooth scroll, scroll-reveal, counters, custom
// cursor. Every piece degrades independently: a missing CDN script or
// prefers-reduced-motion just skips that one effect, never breaks the page.
(function () {
  "use strict";

  var prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  var hasFinePointer = window.matchMedia("(pointer: fine)").matches;

  // ---- Smooth scroll (Lenis) ----
  // Driven from exactly one requestAnimationFrame source: GSAP's ticker when
  // GSAP is present (so ScrollTrigger and Lenis share one clock), otherwise
  // a plain rAF loop. Driving lenis.raf() from two loops at once desyncs its
  // internal timing and freezes the visible scroll while scrollY keeps moving.
  var lenis = null;
  if (!prefersReducedMotion && typeof window.Lenis === "function") {
    lenis = new window.Lenis({ duration: 1.1, smoothWheel: true });
  }

  // ---- GSAP ScrollTrigger: word-by-word statement fill ----
  if (!prefersReducedMotion && window.gsap && window.ScrollTrigger) {
    gsap.registerPlugin(ScrollTrigger);
    if (lenis) {
      lenis.on("scroll", ScrollTrigger.update);
      gsap.ticker.add(function (time) {
        lenis.raf(time * 1000);
      });
      gsap.ticker.lagSmoothing(0);
    }

    document.querySelectorAll(".statement-text").forEach(function (el) {
      var words = el.querySelectorAll(".word");
      if (!words.length) return;
      gsap.to(words, {
        color: "var(--text)",
        stagger: 1,
        scrollTrigger: {
          trigger: el,
          start: "top 75%",
          end: "bottom 55%",
          scrub: true,
        },
      });
    });

    // ---- Banner scroll parallax (apple.com-style product depth) ----
    // Each homepage banner's photo drifts and grows slightly as it scrolls
    // through the viewport.
    document.querySelectorAll(".home-banner").forEach(function (banner) {
      var img = banner.querySelector(".home-banner-media img");
      if (!img) return;
      gsap.fromTo(
        img,
        { scale: 0.94, yPercent: 6 },
        {
          scale: 1.06,
          yPercent: -6,
          ease: "none",
          scrollTrigger: { trigger: banner, start: "top bottom", end: "bottom top", scrub: true },
        }
      );
    });
  } else {
    // No GSAP or motion disabled: just show the full text, no fill animation.
    document.querySelectorAll(".statement-text .word").forEach(function (w) {
      w.classList.add("is-filled");
    });
    // No GSAP ticker to drive Lenis, fall back to a plain rAF loop.
    if (lenis) {
      (function rafLoop(time) {
        lenis.raf(time);
        requestAnimationFrame(rafLoop);
      })();
    }
  }

  // ---- IntersectionObserver: card reveal + counters ----
  if ("IntersectionObserver" in window) {
    var revealObserver = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) {
            entry.target.classList.add("is-revealed");
            revealObserver.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.2 }
    );
    document.querySelectorAll(".product-card, .model-tile, .home-banner, .promo-banner").forEach(function (el, i) {
      if (el.classList.contains("product-card") || el.classList.contains("model-tile")) {
        el.style.transitionDelay = (i % 6) * 0.04 + "s";
      }
      revealObserver.observe(el);
    });

    var counterObserver = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (!entry.isIntersecting) return;
          counterObserver.unobserve(entry.target);
          var target = entry.target;
          var end = parseFloat(target.dataset.countTo || "0");
          var suffix = target.dataset.countSuffix || "";
          if (prefersReducedMotion || end === 0) {
            target.textContent = end + suffix;
            return;
          }
          var start = 0;
          var duration = 900;
          var startTime = null;
          function step(ts) {
            if (startTime === null) startTime = ts;
            var progress = Math.min((ts - startTime) / duration, 1);
            var eased = 1 - Math.pow(1 - progress, 3);
            target.textContent = Math.round(start + (end - start) * eased) + suffix;
            if (progress < 1) requestAnimationFrame(step);
          }
          requestAnimationFrame(step);
        });
      },
      { threshold: 0.6 }
    );
    document.querySelectorAll("[data-count-to]").forEach(function (el) {
      counterObserver.observe(el);
    });
  } else {
    document.querySelectorAll(".product-card, .model-tile, .home-banner, .promo-banner").forEach(function (el) {
      el.classList.add("is-revealed");
    });
    document.querySelectorAll("[data-count-to]").forEach(function (el) {
      el.textContent = (el.dataset.countTo || "0") + (el.dataset.countSuffix || "");
    });
  }

  // ---- Homepage carousel (rauvang.com's rotating top banner) ----
  // Auto-advances every 6s, pauses on hover and keyboard focus, and never
  // auto-advances under prefers-reduced-motion. Arrows and dots always work.
  document.querySelectorAll("[data-carousel]").forEach(function (root) {
    var slides = root.querySelectorAll(".home-slide");
    var dots = root.querySelectorAll("[data-carousel-dot]");
    if (slides.length < 2) return;
    var current = 0;
    var timer = null;

    function show(index) {
      current = (index + slides.length) % slides.length;
      slides.forEach(function (s, i) {
        var active = i === current;
        s.classList.toggle("is-active", active);
        s.setAttribute("aria-hidden", active ? "false" : "true");
        s.tabIndex = active ? 0 : -1;
      });
      dots.forEach(function (d, i) { d.classList.toggle("is-active", i === current); });
    }
    function start() {
      if (prefersReducedMotion || timer) return;
      timer = setInterval(function () { show(current + 1); }, 6000);
    }
    function stop() {
      clearInterval(timer);
      timer = null;
    }

    root.querySelector("[data-carousel-prev]").addEventListener("click", function () { show(current - 1); });
    root.querySelector("[data-carousel-next]").addEventListener("click", function () { show(current + 1); });
    dots.forEach(function (d) {
      d.addEventListener("click", function () { show(parseInt(d.dataset.carouselDot, 10)); });
    });
    root.addEventListener("mouseenter", stop);
    root.addEventListener("mouseleave", start);
    root.addEventListener("focusin", stop);
    root.addEventListener("focusout", start);
    start();
  });

  // ---- Nav search toggle (rauvang.com opens a search field under the bar) ----
  var searchToggle = document.querySelector("[data-search-toggle]");
  var searchForm = document.getElementById("site-search");
  if (searchToggle && searchForm) {
    searchToggle.addEventListener("click", function () {
      var open = searchForm.hidden;
      searchForm.hidden = !open;
      searchToggle.setAttribute("aria-expanded", open ? "true" : "false");
      if (open) searchForm.querySelector("input").focus();
    });
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape" && !searchForm.hidden) {
        searchForm.hidden = true;
        searchToggle.setAttribute("aria-expanded", "false");
        searchToggle.focus();
      }
    });
  }

  // ---- Back-to-top button (rauvang.com's round button, bottom right) ----
  var toTop = document.querySelector("[data-back-to-top]");
  if (toTop) {
    var updateToTop = function () { toTop.hidden = window.scrollY < 480; };
    window.addEventListener("scroll", updateToTop, { passive: true });
    updateToTop();
    toTop.addEventListener("click", function () {
      if (lenis) lenis.scrollTo(0);
      else window.scrollTo({ top: 0, behavior: prefersReducedMotion ? "auto" : "smooth" });
    });
  }

  // ---- Variant page: colour and region choices (rauvang.com's swatches) ----
  // Each chip is a real link, so choosing works without this script. With
  // it, the price, SKU, and stock swap in place and the URL keeps the choice.
  document.querySelectorAll("[data-config-choices]").forEach(function (root) {
    var choices;
    try {
      choices = JSON.parse(root.getAttribute("data-config-choices"));
    } catch (e) {
      return;
    }
    if (!choices.length) return;
    var colorChips = root.querySelectorAll("[data-choice-color]");
    var regionChips = root.querySelectorAll("[data-choice-region]");
    var priceEl = root.querySelector("[data-choice-price]");
    var wasEl = root.querySelector("[data-choice-was]");
    var promoEl = root.querySelector("[data-choice-promo]");
    var skuEl = root.querySelector("[data-choice-sku]");
    var stockEl = root.querySelector("[data-choice-stock]");
    var variantInput = root.querySelector("[data-choice-variant]");
    var returnInputs = root.querySelectorAll("[data-choice-return]");
    var buyButton = root.querySelector("[data-choice-buy]");
    var signInLink = root.querySelector("[data-choice-signin]");
    var selected = choices.filter(function (c) { return c.sku === (skuEl && skuEl.textContent.trim()); })[0] || choices[0];

    function pick(color, region) {
      return choices.filter(function (c) { return c.color === color && c.region === region; })[0]
        || choices.filter(function (c) { return c.color === color; })[0]
        || choices[0];
    }
    function render() {
      colorChips.forEach(function (chip) {
        var on = chip.dataset.choiceColor === selected.color;
        chip.classList.toggle("is-selected", on);
        if (on) chip.setAttribute("aria-current", "true"); else chip.removeAttribute("aria-current");
      });
      regionChips.forEach(function (chip) {
        var region = chip.dataset.choiceRegion;
        var on = region === selected.region;
        var available = choices.some(function (c) { return c.color === selected.color && c.region === region; });
        chip.classList.toggle("is-selected", on);
        chip.classList.toggle("is-unavailable", !available);
        if (on) chip.setAttribute("aria-current", "true"); else chip.removeAttribute("aria-current");
      });
      if (priceEl) priceEl.textContent = selected.price;
      if (wasEl) { wasEl.textContent = selected.was || ""; wasEl.hidden = !selected.was; }
      if (promoEl) { promoEl.textContent = selected.promo || ""; promoEl.hidden = !selected.promo; }
      if (skuEl) skuEl.textContent = selected.sku;
      if (stockEl) {
        stockEl.textContent = selected.stock > 0 ? selected.stock + " in stock" : "Out of stock";
        stockEl.classList.toggle("is-in-stock", selected.stock > 0);
        stockEl.classList.toggle("is-out-of-stock", selected.stock <= 0);
      }
      var url = new URL(window.location.href);
      if (selected.color) url.searchParams.set("color", selected.color); else url.searchParams.delete("color");
      if (selected.region) url.searchParams.set("region", selected.region); else url.searchParams.delete("region");
      history.replaceState(null, "", url);
      // The cart and compare forms come back to this choice.
      var here = url.pathname + url.search;
      if (variantInput) variantInput.value = selected.id;
      returnInputs.forEach(function (input) { input.value = here; });
      if (buyButton) buyButton.disabled = !selected.buyable;
      if (signInLink) signInLink.setAttribute("href", "/Account/Login?returnUrl=" + encodeURIComponent(here));
    }
    colorChips.forEach(function (chip) {
      chip.addEventListener("click", function (e) {
        e.preventDefault();
        selected = pick(chip.dataset.choiceColor, selected.region);
        render();
      });
    });
    regionChips.forEach(function (chip) {
      chip.addEventListener("click", function (e) {
        e.preventDefault();
        var region = chip.dataset.choiceRegion;
        // A region this colour isn't sold in switches to the first colour
        // that is, rather than ignoring the click.
        selected = choices.filter(function (c) { return c.color === selected.color && c.region === region; })[0]
          || choices.filter(function (c) { return c.region === region; })[0]
          || selected;
        render();
      });
    });
  });

  // ---- Custom cursor: desktop, fine pointer, motion allowed only ----
  if (!prefersReducedMotion && hasFinePointer) {
    var ring = document.createElement("div");
    ring.className = "cursor-ring";
    var dot = document.createElement("div");
    dot.className = "cursor-dot";
    document.body.appendChild(ring);
    document.body.appendChild(dot);
    document.body.classList.add("has-custom-cursor");

    var ringPos = { x: window.innerWidth / 2, y: window.innerHeight / 2 };
    var target = { x: ringPos.x, y: ringPos.y };

    window.addEventListener("mousemove", function (e) {
      target.x = e.clientX;
      target.y = e.clientY;
      dot.style.transform = "translate(" + e.clientX + "px," + e.clientY + "px) translate(-50%,-50%)";
    });

    document.querySelectorAll("a, button").forEach(function (el) {
      el.addEventListener("mouseenter", function () { ring.classList.add("is-active"); });
      el.addEventListener("mouseleave", function () { ring.classList.remove("is-active"); });
    });

    function animateRing() {
      ringPos.x += (target.x - ringPos.x) * 0.18;
      ringPos.y += (target.y - ringPos.y) * 0.18;
      ring.style.transform = "translate(" + ringPos.x + "px," + ringPos.y + "px) translate(-50%,-50%)";
      requestAnimationFrame(animateRing);
    }
    requestAnimationFrame(animateRing);
  }
})();
