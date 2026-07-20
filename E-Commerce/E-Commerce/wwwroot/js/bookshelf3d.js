// ============================================================================
// Okean Kitabevi — Virtual 3D Kitab Rəfi
// Three.js ilə interaktiv, immersive kitab rəfi təcrübəsi.
// Hər bir 3D kitab obyekti mesh.userData.productId vasitəsilə verilənlər
// bazasındakı unikal Product.Id-yə bağlanır (Smart ID Mapping).
// ============================================================================

import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";

const SPINE_PALETTE = [
    "#7a1f2b", "#0b2f6b", "#1f6f4a", "#8a5a1a",
    "#4a2e6b", "#1a5f6b", "#6b1a4a", "#2e4a1a",
];

function hashColor(seed) {
    let h = 0;
    for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) >>> 0;
    return SPINE_PALETTE[h % SPINE_PALETTE.length];
}

export function initBookshelf3D(containerId, dataScriptId, onReady) {
    const container = document.getElementById(containerId);
    if (!container) return;

    const dataEl = document.getElementById(dataScriptId);
    let books = [];
    try {
        books = JSON.parse(dataEl?.textContent || "[]");
    } catch (e) {
        console.error("Rəf məlumatları oxuna bilmədi:", e);
    }

    const overlay = container.querySelector(".shelf3d-loading");
    const hint = container.querySelector(".shelf3d-hint");

    if (!books.length) {
        if (overlay) overlay.innerHTML = "<span>Hazırda göstəriləcək kitab yoxdur.</span>";
        if (onReady) onReady();
        return;
    }


    // ---- Səhnə qurulması --------------------------------------------------
    const scene = new THREE.Scene();
    scene.background = null;

    const camera = new THREE.PerspectiveCamera(
        38,
        container.clientWidth / container.clientHeight,
        0.1,
        100
    );
    camera.position.set(0, 1.4, 8.5);

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    if ("outputColorSpace" in renderer) renderer.outputColorSpace = THREE.SRGBColorSpace;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.enablePan = false;
    controls.minDistance = 5.5;
    controls.maxDistance = 11;
    controls.minPolarAngle = Math.PI / 2 - 0.35;
    controls.maxPolarAngle = Math.PI / 2 + 0.15;
    controls.minAzimuthAngle = -0.7;
    controls.maxAzimuthAngle = 0.7;
    controls.target.set(0, 1.1, 0);
    controls.update();

    // ---- İşıqlandırma -------------------------------------------------------
    scene.add(new THREE.AmbientLight(0xfff2df, 0.55));

    const keyLight = new THREE.DirectionalLight(0xfff6e6, 1.15);
    keyLight.position.set(4, 6, 5);
    keyLight.castShadow = true;
    keyLight.shadow.mapSize.set(1024, 1024);
    keyLight.shadow.camera.left = -6;
    keyLight.shadow.camera.right = 6;
    keyLight.shadow.camera.top = 6;
    keyLight.shadow.camera.bottom = -6;
    scene.add(keyLight);

    const warmLamp = new THREE.PointLight(0xffb066, 0.9, 12);
    warmLamp.position.set(-3, 3, 4);
    scene.add(warmLamp);

    const rim = new THREE.DirectionalLight(0x9ec8ff, 0.35);
    rim.position.set(-5, 2, -4);
    scene.add(rim);

    // ---- Taxta rəf (shelf) ---------------------------------------------------
    const woodMat = new THREE.MeshStandardMaterial({ color: 0x3f2a1a, roughness: 0.75, metalness: 0.05 });
    const woodMatLight = new THREE.MeshStandardMaterial({ color: 0x5a3c24, roughness: 0.7, metalness: 0.05 });

    const shelfGroup = new THREE.Group();
    const shelfWidth = 8.6;
    const shelfDepth = 1.05;
    const boardH = 0.12;

    function makeBoard(w, h, d, mat, x, y, z) {
        const board = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), mat);
        board.position.set(x, y, z);
        board.receiveShadow = true;
        board.castShadow = true;
        return board;
    }

    // üst, orta, alt rəf lövhələri + yan dirəklər + arxa panel
    shelfGroup.add(makeBoard(shelfWidth, boardH, shelfDepth, woodMatLight, 0, 2.15, 0));
    shelfGroup.add(makeBoard(shelfWidth, boardH, shelfDepth, woodMatLight, 0, 0.05, 0));
    shelfGroup.add(makeBoard(shelfWidth, boardH, shelfDepth, woodMatLight, 0, -2.05, 0));
    shelfGroup.add(makeBoard(0.15, 4.3, shelfDepth, woodMat, -shelfWidth / 2 - 0.02, 0.05, 0));
    shelfGroup.add(makeBoard(0.15, 4.3, shelfDepth, woodMat, shelfWidth / 2 + 0.02, 0.05, 0));
    const back = makeBoard(shelfWidth, 4.3, 0.08, woodMat, 0, 0.05, -shelfDepth / 2);
    back.material = new THREE.MeshStandardMaterial({ color: 0x241708, roughness: 0.9 });
    shelfGroup.add(back);

    scene.add(shelfGroup);

    // ---- Kitablar -------------------------------------------------------------
    const textureLoader = new THREE.TextureLoader();
    textureLoader.crossOrigin = "anonymous";

    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    const bookMeshes = [];
    const tweenState = new Map(); // mesh.uuid -> { start, target, duration, onComplete }

    const row1Y = 1.05; // üst rəf üzərindəki kitablar
    const row2Y = -1.05; // orta rəf üzərindəki kitablar

    const rows = [[], []];
    books.forEach((b, i) => rows[i % 2].push(b));

    function layoutRow(rowBooks, y) {
        const bookW = 0.62;
        const gap = 0.08;
        const totalW = rowBooks.length * (bookW + gap) - gap;
        let x = -totalW / 2 + bookW / 2;

        rowBooks.forEach((book) => {
            const height = 1.35 + (Math.abs(hashSeed(book.title)) % 20) / 100; // yüngül hündürlük fərqi
            const depth = 0.14;
            const geo = new THREE.BoxGeometry(bookW, height, depth);

            const spineColor = new THREE.Color(hashColor(String(book.title || book.id)));
            const spineMat = new THREE.MeshStandardMaterial({ color: spineColor, roughness: 0.55 });
            const pageMat = new THREE.MeshStandardMaterial({ color: 0xece2c9, roughness: 0.9 });

            // Materials sırası: [+x,-x,+y,-y,+z(ön/cover),-z(arxa)]
            const materials = [spineMat, spineMat, pageMat, pageMat, spineMat.clone(), spineMat.clone()];
            const mesh = new THREE.Mesh(geo, materials);
            mesh.castShadow = true;
            mesh.receiveShadow = true;
            mesh.position.set(x, y + height / 2 - 1.0 + 1.0, 0.05);
            mesh.userData = {
                productId: book.id,
                title: book.title,
                baseX: x,
                baseY: mesh.position.y,
                baseZ: mesh.position.z,
                baseRotY: 0,
                isAnimating: false,
                hovered: false,
            };

            shelfGroup.add(mesh);
            bookMeshes.push(mesh);

            // Örtük (cover) tekstura kimi ön üzə yüklənir
            if (book.imageUrl) {
                textureLoader.load(
                    book.imageUrl,
                    (tex) => {
                        if ("colorSpace" in tex) tex.colorSpace = THREE.SRGBColorSpace;
                        const coverMat = new THREE.MeshStandardMaterial({ map: tex, roughness: 0.4 });
                        mesh.material[4] = coverMat; // +z üz = ön qapaq
                        mesh.material.needsUpdate = true;
                    },
                    undefined,
                    () => {
                        // şəkil tapılmadıqda spine rəngi ilə fallback edilir (artıq təyin olunub)
                    }
                );
            }

            x += bookW + gap;
        });
    }

    function hashSeed(str) {
        let h = 0;
        const s = String(str || "");
        for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) | 0;
        return h;
    }

    layoutRow(rows[0], row1Y);
    layoutRow(rows[1], row2Y);

    // ---- Qarşılıqlı əlaqə (hover + click) --------------------------------
    let hoveredMesh = null;

    function setPointerFromEvent(evt) {
        const rect = renderer.domElement.getBoundingClientRect();
        pointer.x = ((evt.clientX - rect.left) / rect.width) * 2 - 1;
        pointer.y = -((evt.clientY - rect.top) / rect.height) * 2 + 1;
    }

    function animateMesh(mesh, target, duration, onComplete) {
        tweenState.set(mesh.uuid, {
            mesh,
            start: {
                x: mesh.position.x, y: mesh.position.y, z: mesh.position.z,
                rotY: mesh.rotation.y,
            },
            target,
            t0: performance.now(),
            duration,
            onComplete,
        });
    }

    function onPointerMove(evt) {
        setPointerFromEvent(evt);
        raycaster.setFromCamera(pointer, camera);
        const hits = raycaster.intersectObjects(bookMeshes, false);

        if (hits.length > 0) {
            const mesh = hits[0].object;
            renderer.domElement.style.cursor = "pointer";
            if (hoveredMesh !== mesh && !mesh.userData.isAnimating) {
                if (hoveredMesh && !hoveredMesh.userData.isAnimating) resetHover(hoveredMesh);
                hoveredMesh = mesh;
                mesh.userData.hovered = true;
                animateMesh(mesh, {
                    x: mesh.userData.baseX,
                    y: mesh.userData.baseY + 0.16,
                    z: mesh.userData.baseZ + 0.28,
                    rotY: 0,
                }, 220);
                if (hint) hint.textContent = `📖 ${mesh.userData.title}`;
            }
        } else {
            renderer.domElement.style.cursor = "grab";
            if (hoveredMesh && !hoveredMesh.userData.isAnimating) {
                resetHover(hoveredMesh);
                hoveredMesh = null;
                if (hint) hint.textContent = "Bir kitabı seçin →";
            }
        }
    }

    function resetHover(mesh) {
        mesh.userData.hovered = false;
        animateMesh(mesh, {
            x: mesh.userData.baseX,
            y: mesh.userData.baseY,
            z: mesh.userData.baseZ,
            rotY: 0,
        }, 220);
    }

    function onClick(evt) {
        setPointerFromEvent(evt);
        raycaster.setFromCamera(pointer, camera);
        const hits = raycaster.intersectObjects(bookMeshes, false);
        if (hits.length === 0) return;

        const mesh = hits[0].object;
        if (mesh.userData.isAnimating) return;
        mesh.userData.isAnimating = true;
        if (hint) hint.textContent = `Açılır: ${mesh.userData.title}...`;

        // "Rəfdən götürülmə" animasiyası: kitab önə çıxır, yüngül fırlanır, sonra keçid edilir
        animateMesh(mesh, {
            x: mesh.userData.baseX,
            y: mesh.userData.baseY + 0.55,
            z: mesh.userData.baseZ + 1.7,
            rotY: 0.35,
        }, 480, () => {
            window.location.href = `/Product/Details/${mesh.userData.productId}`;
        });
    }

    renderer.domElement.addEventListener("pointermove", onPointerMove);
    renderer.domElement.addEventListener("click", onClick);
    renderer.domElement.addEventListener("pointerleave", () => {
        if (hoveredMesh && !hoveredMesh.userData.isAnimating) {
            resetHover(hoveredMesh);
            hoveredMesh = null;
        }
    });

    // ---- Ölçü tənzimi -----------------------------------------------------
    const resizeObserver = new ResizeObserver(() => {
        const w = container.clientWidth;
        const h = container.clientHeight;
        if (w === 0 || h === 0) return;
        camera.aspect = w / h;
        camera.updateProjectionMatrix();
        renderer.setSize(w, h);
    });
    resizeObserver.observe(container);

    // ---- Render dövrü -------------------------------------------------------
    function easeOutCubic(t) {
        return 1 - Math.pow(1 - t, 3);
    }

    function updateTweens(now) {
        for (const [uuid, tw] of tweenState) {
            const t = Math.min(1, (now - tw.t0) / tw.duration);
            const e = easeOutCubic(t);
            tw.mesh.position.x = tw.start.x + (tw.target.x - tw.start.x) * e;
            tw.mesh.position.y = tw.start.y + (tw.target.y - tw.start.y) * e;
            tw.mesh.position.z = tw.start.z + (tw.target.z - tw.start.z) * e;
            tw.mesh.rotation.y = tw.start.rotY + (tw.target.rotY - tw.start.rotY) * e;
            if (t >= 1) {
                tweenState.delete(uuid);
                if (tw.onComplete) tw.onComplete();
            }
        }
    }

    let loaded = false;
    function animate(now) {
        requestAnimationFrame(animate);
        updateTweens(now || performance.now());
        controls.update();
        renderer.render(scene, camera);
        if (!loaded) {
            loaded = true;
            if (overlay) overlay.classList.add("shelf3d-hidden");
            if (onReady) onReady();
        }
    }
    requestAnimationFrame(animate);
}
