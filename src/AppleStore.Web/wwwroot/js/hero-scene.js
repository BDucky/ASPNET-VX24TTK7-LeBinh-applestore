// Stylized, original 3D hero visual: a small orbiting cluster of abstract
// device glyphs (phone / tablet / watch-ring / earbuds), not any real
// product's geometry or textures. Skips entirely without WebGL, without
// Three.js, or under prefers-reduced-motion, the CSS gradient in
// .hero::before carries the hero on its own in that case.
(function () {
  "use strict";

  var canvas = document.getElementById("hero-canvas");
  if (!canvas) return;
  if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;
  if (typeof window.THREE === "undefined") return;

  function hasWebGL() {
    try {
      var c = document.createElement("canvas");
      return !!(window.WebGLRenderingContext && (c.getContext("webgl") || c.getContext("experimental-webgl")));
    } catch (e) {
      return false;
    }
  }
  if (!hasWebGL()) return;

  var THREE = window.THREE;
  var container = canvas.parentElement;

  var scene = new THREE.Scene();
  var camera = new THREE.PerspectiveCamera(38, container.clientWidth / container.clientHeight, 0.1, 100);
  camera.position.set(0, 0, 6.2);

  var renderer = new THREE.WebGLRenderer({ canvas: canvas, alpha: true, antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.setSize(container.clientWidth, container.clientHeight);
  renderer.setClearColor(0x000000, 0);

  scene.add(new THREE.AmbientLight(0x8891ff, 0.55));
  var key = new THREE.DirectionalLight(0xffffff, 1.1);
  key.position.set(3, 4, 5);
  scene.add(key);
  var rim = new THREE.PointLight(0x4f7fff, 2.2, 12);
  rim.position.set(-3, -1, 2);
  scene.add(rim);

  var metal = new THREE.MeshStandardMaterial({ color: 0xe7e7ec, metalness: 0.65, roughness: 0.28 });
  var screen = new THREE.MeshStandardMaterial({
    color: 0x0a0a0c,
    emissive: 0x4f7fff,
    emissiveIntensity: 0.55,
    metalness: 0.1,
    roughness: 0.4,
  });

  var group = new THREE.Group();

  // Phone
  var phone = new THREE.Group();
  var phoneBody = new THREE.Mesh(new THREE.BoxGeometry(0.62, 1.28, 0.07), metal);
  var phoneScreen = new THREE.Mesh(new THREE.PlaneGeometry(0.52, 1.14), screen);
  phoneScreen.position.z = 0.037;
  phone.add(phoneBody, phoneScreen);
  phone.position.set(-1.2, 0.1, 0.4);
  phone.rotation.set(0.1, 0.5, -0.08);

  // Tablet
  var tablet = new THREE.Group();
  var tabletBody = new THREE.Mesh(new THREE.BoxGeometry(1.0, 1.34, 0.06), metal);
  var tabletScreen = new THREE.Mesh(new THREE.PlaneGeometry(0.9, 1.2), screen);
  tabletScreen.position.z = 0.033;
  tablet.add(tabletBody, tabletScreen);
  tablet.position.set(1.15, -0.35, -0.6);
  tablet.rotation.set(-0.06, -0.45, 0.05);

  // Watch (ring + face)
  var watch = new THREE.Group();
  var watchFace = new THREE.Mesh(new THREE.CylinderGeometry(0.34, 0.34, 0.09, 48), metal);
  watchFace.rotation.x = Math.PI / 2;
  var watchScreen = new THREE.Mesh(new THREE.CircleGeometry(0.27, 48), screen);
  watchScreen.position.z = 0.046;
  watch.add(watchFace, watchScreen);
  watch.position.set(0.55, 1.0, 0.2);
  watch.rotation.set(0.3, 0.2, 0);

  // Earbuds (two small capsules approximated with sphere + cylinder)
  function earbud() {
    var bud = new THREE.Group();
    var head = new THREE.Mesh(new THREE.SphereGeometry(0.11, 24, 24), metal);
    var stem = new THREE.Mesh(new THREE.CylinderGeometry(0.035, 0.05, 0.26, 16), metal);
    stem.position.set(0.03, -0.17, 0);
    stem.rotation.z = -0.25;
    bud.add(head, stem);
    return bud;
  }
  var budA = earbud();
  budA.position.set(-0.55, -1.05, 0.5);
  var budB = earbud();
  budB.position.set(-0.3, -1.2, 0.5);
  budB.rotation.z = 0.4;

  group.add(phone, tablet, watch, budA, budB);
  scene.add(group);

  var mouse = { x: 0, y: 0 };
  window.addEventListener("mousemove", function (e) {
    mouse.x = (e.clientX / window.innerWidth) * 2 - 1;
    mouse.y = (e.clientY / window.innerHeight) * 2 - 1;
  });

  function onResize() {
    var w = container.clientWidth;
    var h = container.clientHeight;
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    renderer.setSize(w, h);
  }
  window.addEventListener("resize", onResize);

  var clock = new THREE.Clock();
  function tick() {
    var t = clock.getElapsedTime();
    group.rotation.y = t * 0.12 + mouse.x * 0.35;
    group.rotation.x = mouse.y * 0.18;
    group.position.y = Math.sin(t * 0.6) * 0.06;
    requestAnimationFrame(tick);
    renderer.render(scene, camera);
  }
  tick();

  canvas.classList.add("is-ready");
})();
