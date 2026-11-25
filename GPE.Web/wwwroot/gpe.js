window.gpe = {
    // Tras marcar los errores al guardar, lleva la página al primero y pone el cursor en él
    irAlPrimerError: () => {
        const campo = document.querySelector(".con-error input, .con-error textarea, tr:has(.error-campo:not(:empty)) input");
        if (!campo) return;
        campo.scrollIntoView({ block: "center" });
        campo.focus();
    },

    // Al añadir un palet, el cursor va a su peso con el valor seleccionado (para escribir encima)
    irAlUltimoPalet: () => {
        const peso = document.querySelector(".palets tbody tr:last-child input");
        if (!peso) return;
        peso.scrollIntoView({ block: "center" });
        peso.focus();
        peso.select();
    },

    // Incidencia que se está escribiendo desde el control diario, guardada en el propio móvil.
    // Al hacer una foto el móvil puede recargar la página (sobre todo Android, si va justo de
    // memoria): así, al volver, se recupera lo escrito y las fotos. Los datos van en localStorage
    // y las fotos en IndexedDB (no caben en localStorage). Se borra al guardar o al cerrar la ficha.
    borradorNc: {
        prefijo: "gpe-borrador-nc|",

        guardar: (clave, json) => localStorage.setItem(gpe.borradorNc.prefijo + clave, json),

        leer: (clave) => localStorage.getItem(gpe.borradorNc.prefijo + clave),

        // La clave del borrador que haya (solo se escribe uno a la vez), o null
        pendiente: () => {
            for (let i = 0; i < localStorage.length; i++) {
                const k = localStorage.key(i);
                if (k.startsWith(gpe.borradorNc.prefijo)) return k.substring(gpe.borradorNc.prefijo.length);
            }
            return null;
        },

        borrar: async (clave) => {
            localStorage.removeItem(gpe.borradorNc.prefijo + clave);
            const fotos = await gpe.borradorNc.leerFotos(clave);
            await gpe.borradorNc.base(db => {
                const almacen = db.transaction("fotos", "readwrite").objectStore("fotos");
                fotos.forEach(f => almacen.delete(f.id));
            });
        },

        // Nombre y tamaño de las fotos guardadas (con eso se reconocen al quitar una)
        fotos: async (clave) =>
            (await gpe.borradorNc.leerFotos(clave)).map(f => ({ nombre: f.nombre, tamano: f.blob.size })),

        // El contenido de la foto número i, para pasarlo al servidor
        foto: async (clave, i) => {
            const fotos = await gpe.borradorNc.leerFotos(clave);
            return new Uint8Array(await fotos[i].blob.arrayBuffer());
        },

        quitarFoto: async (clave, nombre, tamano) => {
            const foto = (await gpe.borradorNc.leerFotos(clave)).find(f => f.nombre === nombre && f.blob.size === tamano);
            if (foto) await gpe.borradorNc.base(db => db.transaction("fotos", "readwrite").objectStore("fotos").delete(foto.id));
        },

        leerFotos: (clave) => gpe.borradorNc.base(db => new Promise((bien, mal) => {
            const peticion = db.transaction("fotos").objectStore("fotos").getAll();
            peticion.onsuccess = () => bien(peticion.result.filter(f => f.clave === clave).sort((a, b) => a.id - b.id));
            peticion.onerror = () => mal(peticion.error);
        })),

        base: (accion) => new Promise((bien, mal) => {
            const peticion = indexedDB.open("gpe", 1);
            peticion.onupgradeneeded = () => peticion.result.createObjectStore("fotos", { keyPath: "id", autoIncrement: true });
            peticion.onerror = () => mal(peticion.error);
            peticion.onsuccess = async () => {
                const db = peticion.result;
                try { bien(await accion(db)); } catch (e) { mal(e); } finally { db.close(); }
            };
        })
    }
};

// Cada foto elegida en la ficha se copia al momento en el móvil, antes incluso de que llegue al
// servidor: si la página se ha recargado al volver de la cámara, la foto no se pierde.
document.addEventListener("change", e => {
    const entrada = e.target;
    if (!(entrada instanceof HTMLInputElement) || entrada.type !== "file") return;
    const clave = entrada.closest("[data-borrador]")?.dataset.borrador;
    if (!clave || !entrada.files) return;
    const fotos = [...entrada.files].filter(f => f.type.startsWith("image/")).slice(0, 20);
    gpe.borradorNc.base(db => {
        const almacen = db.transaction("fotos", "readwrite").objectStore("fotos");
        fotos.forEach(f => almacen.add({ clave, nombre: f.name, blob: f }));
    }).catch(() => { });
}, true);
