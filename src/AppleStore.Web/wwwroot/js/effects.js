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
    document.querySelectorAll(".category-card").forEach(function (el) {
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
    document.querySelectorAll(".category-card").forEach(function (el) {
      el.classList.add("is-revealed");
    });
    document.querySelectorAll("[data-count-to]").forEach(function (el) {
      el.textContent = (el.dataset.countTo || "0") + (el.dataset.countSuffix || "");
    });
  }

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

    document.querySelectorAll("a, button, .category-card").forEach(function (el) {
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
