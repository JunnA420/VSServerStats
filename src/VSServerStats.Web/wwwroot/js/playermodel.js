// VS Seraph model renderer for Three.js

(function () {
    const renderers = {};

    window.initPlayerModel = async function (canvasId, appearanceData) {
        if (renderers[canvasId]) {
            renderers[canvasId].dispose();
            delete renderers[canvasId];
        }

        for (let i = 0; i < 50; i++) {
            if (window.THREE?.OrbitControls) break;
            await new Promise(r => setTimeout(r, 100));
        }
        if (!window.THREE?.OrbitControls) { console.error('[PlayerModel] THREE.OrbitControls not loaded'); return; }

        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        for (let i = 0; i < 20; i++) {
            if (canvas.clientWidth > 0 && canvas.clientHeight > 0) break;
            await new Promise(r => setTimeout(r, 100));
        }
        if (canvas.clientWidth === 0 || canvas.clientHeight === 0) return;

        const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true });
        renderer.setPixelRatio(window.devicePixelRatio);
        renderer.setSize(canvas.clientWidth, canvas.clientHeight);
        renderer.shadowMap.enabled = true;

        const scene = new THREE.Scene();
        const camera = new THREE.PerspectiveCamera(35, canvas.clientWidth / canvas.clientHeight, 0.1, 1000);
        camera.position.set(0, 30, 60);
        camera.lookAt(0, 15, 0);

        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        scene.add(ambientLight);
        const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
        dirLight.position.set(10, 20, 20);
        scene.add(dirLight);

        const controls = new THREE.OrbitControls(camera, renderer.domElement);
        controls.target.set(0, 15, 0);
        controls.enableDamping = true;
        controls.dampingFactor = 0.08;
        controls.minDistance = 20;
        controls.maxDistance = 120;
        controls.update();

        try {
            const shape = await loadJSON('/assets/shapes/entity/humanoid/seraph.json');

            // ── Collect extra shape parts ──────────────────────────────────────
            const partDefs = [];
            if (appearanceData?.hairType)
                partDefs.push({ category: 'hair-base',  path: `hair-base/${appearanceData.hairType}` });
            if (appearanceData?.hairExtra && appearanceData.hairExtra !== 'none')
                partDefs.push({ category: 'hair-extra', path: `hair-extra/${appearanceData.hairExtra}` });
            if (appearanceData?.facialExpression)
                partDefs.push({ category: 'face',       path: `face/${appearanceData.facialExpression}` });
            if (appearanceData?.facialHair && appearanceData.facialHair !== 'none')
                partDefs.push({ category: 'hair-face',  path: `hair-face/${appearanceData.facialHair}` });
            if (appearanceData?.beard && appearanceData.beard !== 'none')
                partDefs.push({ category: 'hair-face',  path: `hair-face/${appearanceData.beard}` });

            const partResults = await Promise.all(
                partDefs.map(d => loadJSON(`/assets/shapes/entity/humanoid/seraphskinparts/${d.path}.json`).catch(() => null))
            );

            // ── Remove default hair/face from base seraph shape ────────────────
            if (appearanceData?.hairType) {
                removeElementsByNames(shape.elements, ['Hair', 'Hair tile upper part']);
            }
            if (appearanceData?.facialExpression) {
                removeElementsByNames(shape.elements, ['eyesroot']);
            }

            // Hair/face part shapes use coordinate origin [0,0,0].
            // buildModel calls buildElement for top-level elements with ox=-8,oy=0,oz=-8.
            // We wrap each injected element in a zero-size pivot at [8,0,8] whose child
            // offset becomes (from-rotOrigin) = (8-8, 0-0, 8-8) = (0,0,0), so the hair
            // element's own rotationOrigin is used as-is without any extra shift.
            let partIdx = 0;
            for (const part of partResults) {
                if (!part?.elements) { partIdx++; continue; }

                for (const el of part.elements) {
                    // Deduplicate names
                    if (shape.elements.some(e => e.name === el.name)) {
                        el.name = `${el.name}__${partIdx}`;
                    }
                    // Wrap in pivot so child offset = 0
                    shape.elements.push({
                        name: `__wrap_${partIdx}_${el.name}`,
                        rotationOrigin: [8, 0, 8],
                        from: [5.240, 25, 5.5],
                        to:   [8, 0, 8],
                        children: [el]
                    });
                }

                if (part.textureSizes) Object.assign(shape.textureSizes || (shape.textureSizes = {}), part.textureSizes);
                if (part.textures)     Object.assign(shape.textures     || (shape.textures = {}),     part.textures);
                partIdx++;
            }

            const textures   = await loadTextures(shape, appearanceData);
            const modelGroup = buildModel(shape, textures);
            modelGroup.rotation.y = Math.PI / 2;
            scene.add(modelGroup);


        } catch (e) {
            console.error('[PlayerModel] Failed to load model:', e);
        }

        let animTime = 0;
        function animate() {
            const id = requestAnimationFrame(animate);
            animTime += 0.016;
            controls.update();
            renderer.render(scene, camera);
            renderers[canvasId]._animId = id;
        }

        renderers[canvasId] = {
            renderer,
            dispose: () => {
                cancelAnimationFrame(renderers[canvasId]?._animId);
                renderer.dispose();
            }
        };

        animate();
    };

    // ── Remove elements by name (recursive) ────────────────────────────────────
    function removeElementsByNames(elements, names) {
        const nameSet = new Set(names);
        for (let i = elements.length - 1; i >= 0; i--) {
            if (nameSet.has(elements[i].name)) {
                elements.splice(i, 1);
            } else if (elements[i].children) {
                removeElementsByNames(elements[i].children, names);
            }
        }
    }

    async function loadJSON(url) {
        const res = await fetch(url);
        if (!res.ok) throw new Error('Failed to fetch ' + url);
        return res.json();
    }

    async function loadTextures(_shape, appearanceData) {
        const loader = new THREE.TextureLoader();
        const textures = {};

        const skinCode = appearanceData?.skinColor || 'skin1';
        const hairCode = appearanceData?.hairColor || 'lightgray';
        const eyeCode  = appearanceData?.eyeColor  || 'jonas';

        const seraphTex = await compositeTexture(
            `/assets/skins/seraphskinparts/body/${skinCode}.png`,
            `/assets/skins/seraphskinparts/eyes/${eyeCode}.png`,
            '/assets/skins/seraphskinparts/body/skin1.png',
            '/assets/skins/seraphskinparts/eyes/jonas.png'
        );

        textures['seraph'] = new THREE.CanvasTexture(seraphTex);
        textures['seraph'].magFilter = THREE.NearestFilter;
        textures['seraph'].minFilter = THREE.NearestFilter;

        const otherPaths = {
            hair:     `/assets/skins/seraphskinparts/hair/${hairCode}.png`,
            reedrope: `/assets/skins/reedrope.png`,
        };
        const otherFallbacks = {
            hair:     '/assets/skins/seraphskinparts/hair/lightgray.png',
            reedrope: '/assets/skins/reedrope.png',
        };

        for (const [key, path] of Object.entries(otherPaths)) {
            textures[key] = await loadTextureWithFallback(loader, path, otherFallbacks[key]);
            textures[key].magFilter = THREE.NearestFilter;
            textures[key].minFilter = THREE.NearestFilter;
        }

        return textures;
    }

    async function compositeTexture(basePath, overlayPath, baseFallback, overlayFallback) {
        const loadImg = (src, fallback) => new Promise(resolve => {
            const img = new Image();
            img.onload = () => resolve(img);
            img.onerror = () => { const f = new Image(); f.onload = () => resolve(f); f.src = fallback; };
            img.src = src;
        });

        const [base, overlay] = await Promise.all([
            loadImg(basePath, baseFallback),
            loadImg(overlayPath, overlayFallback)
        ]);

        const canvas = document.createElement('canvas');
        canvas.width  = base.naturalWidth;
        canvas.height = base.naturalHeight;
        const ctx = canvas.getContext('2d');
        ctx.imageSmoothingEnabled = false;
        ctx.drawImage(base, 0, 0);
        const scaleX = canvas.width / 32;
        const scaleY = canvas.height / 76;
        const eyeUVHeight = 2;
        const srcHeight = Math.round(overlay.naturalHeight * eyeUVHeight / 8);
        ctx.drawImage(overlay, 0, 0, overlay.naturalWidth, srcHeight,
                      28 * scaleX, 0,
                      4  * scaleX, eyeUVHeight * scaleY);
        return canvas;
    }

    function loadTextureWithFallback(loader, path, fallback) {
        return new Promise((resolve) => {
            loader.load(path, resolve, undefined, () => loader.load(fallback, resolve));
        });
    }

    function buildModel(shape, textures) {
        const group = new THREE.Group();
        const materials = buildMaterials(shape, textures);
        if (shape.elements) {
            for (const el of shape.elements) {
                buildElement(el, group, materials, shape, 1.0, -8, 0, -8);
            }
        }
        return group;
    }

    function buildMaterials(_shape, textures) {
        const mats = {};
        for (const [key, tex] of Object.entries(textures)) {
            mats[key] = new THREE.MeshLambertMaterial({ map: tex, transparent: true, alphaTest: 0.1 });
        }
        return mats;
    }

    function buildElement(el, parentGroup, materials, shape, scale, ox, oy, oz) {
        const group = new THREE.Group();
        group.name = el.name;

        const from      = el.from || [0, 0, 0];
        const to        = el.to   || [0, 0, 0];
        const rotOrigin = el.rotationOrigin || [0, 0, 0];
        const rx = THREE.MathUtils.degToRad(el.rotationX || 0);
        const ry = THREE.MathUtils.degToRad(el.rotationY || 0);
        const rz = THREE.MathUtils.degToRad(el.rotationZ || 0);

        group.position.set(
            (rotOrigin[0] + ox) * scale,
            (rotOrigin[1] + oy) * scale,
            (rotOrigin[2] + oz) * scale
        );
        group.rotation.order = 'ZYX';
        group.rotation.set(rx, ry, rz);

        const hasVisibleFace = el.faces && Object.values(el.faces).some(f => f.enabled !== false && f.texture && f.texture !== '#null');
        if (hasVisibleFace) {
            const sx = (to[0] - from[0]) * scale || 0.01;
            const sy = (to[1] - from[1]) * scale || 0.01;
            const sz = (to[2] - from[2]) * scale || 0.01;

            if (Math.abs(sx) > 0 && Math.abs(sy) > 0 && Math.abs(sz) > 0) {
                const cx = (from[0] + to[0]) / 2 - rotOrigin[0];
                const cy = (from[1] + to[1]) / 2 - rotOrigin[1];
                const cz = (from[2] + to[2]) / 2 - rotOrigin[2];

                const geometry = buildBoxGeometry(el, from, to, shape);
                const texKey   = getTexKey(el);
                const material = materials[texKey] || Object.values(materials)[0];

                const mesh = new THREE.Mesh(geometry, material);
                mesh.position.set(cx * scale, cy * scale, cz * scale);
                group.add(mesh);
            }
        }

        if (el.children) {
            for (const child of el.children) {
                buildElement(child, group, materials, shape, scale,
                    from[0] - rotOrigin[0],
                    from[1] - rotOrigin[1],
                    from[2] - rotOrigin[2]
                );
            }
        }

        parentGroup.add(group);
    }

    function getTexKey(el) {
        for (const face of Object.values(el.faces || {})) {
            if (face.enabled === false) continue;
            if (!face.texture || face.texture === '#null') continue;
            return face.texture.replace('#', '');
        }
        return 'seraph';
    }

    function buildBoxGeometry(el, from, to, shape) {
        const sx = to[0] - from[0];
        const sy = to[1] - from[1];
        const sz = to[2] - from[2];

        const geo     = new THREE.BoxGeometry(sx, sy, sz);
        const faceMap = ['east', 'west', 'up', 'down', 'south', 'north'];
        const uvAttr  = geo.attributes.uv;

        const texKey   = getTexKey(el);
        const texSize  = shape.textureSizes?.[texKey];
        const texWidth = texSize ? texSize[0] : (shape.textureWidth  || 32);
        const texHeight= texSize ? texSize[1] : (shape.textureHeight || 76);

        for (let fi = 0; fi < 6; fi++) {
            const faceName = faceMap[fi];
            const faceData = el.faces?.[faceName];

            let u0, v0, u1, v1;
            if (faceData?.uv && faceData.enabled !== false && faceData.texture !== '#null') {
                u0 = faceData.uv[0] / texWidth;
                v0 = 1.0 - faceData.uv[3] / texHeight;
                u1 = faceData.uv[2] / texWidth;
                v1 = 1.0 - faceData.uv[1] / texHeight;
            } else {
                u0 = 0; v0 = 0; u1 = 0.001; v1 = 0.001;
            }

            const rotation = faceData?.rotation || 0;
            const uvs      = applyUVRotation(u0, v0, u1, v1, rotation);
            const base     = fi * 4;
            uvAttr.setXY(base + 0, uvs[0], uvs[1]);
            uvAttr.setXY(base + 1, uvs[2], uvs[3]);
            uvAttr.setXY(base + 2, uvs[4], uvs[5]);
            uvAttr.setXY(base + 3, uvs[6], uvs[7]);
        }

        uvAttr.needsUpdate = true;
        return geo;
    }

    function applyUVRotation(u0, v0, u1, v1, rotation) {
        let corners = [
            [u0, v1],
            [u1, v1],
            [u0, v0],
            [u1, v0],
        ];
        const steps = ((rotation / 90) % 4 + 4) % 4;
        for (let i = 0; i < steps; i++) {
            corners = [corners[2], corners[0], corners[3], corners[1]];
        }
        return corners.flat();
    }

})();
