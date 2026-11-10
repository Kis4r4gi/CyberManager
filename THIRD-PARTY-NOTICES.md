# Avisos de terceros

CyberManager se distribuye bajo la licencia **MIT**. Los instaladores pueden incluir,
de forma opcional, el programa **TightVNC**, que se distribuye como programa separado
bajo la **GNU General Public License versión 2 (GPLv2)**.

## TightVNC 2.8.90 (GPLv2)

TightVNC es un programa de control remoto de código abierto. CyberManager lo usa
únicamente como componente opcional:

- En las **estaciones** se puede instalar el **servidor VNC** para que la consola
  abra una sesión VNC cuando el control remoto integrado no sea suficiente.
- En el **servidor** se puede instalar solo el **visor VNC** para abrir las sesiones
  desde la consola de administración.

TightVNC se instala y se ejecuta como un **programa independiente**. CyberManager no
enlaza, modifica ni redistribuye el código de TightVNC dentro de sus propios binarios.
La integración se limita a ejecutar el instalador oficial y el visor como procesos
separados.

### Información de la versión

- **Programa:** TightVNC 2.8.90 (GPL)
- **Sitio oficial:** <https://www.tightvnc.com/>
- **Licencia:** GNU General Public License versión 2. El texto completo se incluye en
  [`LICENSE-TightVNC-GPLv2.txt`](LICENSE-TightVNC-GPLv2.txt).
- **Código fuente oficial:**
  <https://www.tightvnc.com/download/2.8.90/tightvnc-2.8.90-src-gpl.zip>
- **SHA-256 del código fuente:**
  `F3F76393A5865E1F1919C78C3EB43B9C937CF738927C2BFC920ECBD3913B061C`
- **Tamaño del código fuente:** 3.018.194 bytes

Los instaladores MSI se distribuyen **sin modificaciones** y se descargan con
`scripts\download-tightvnc.ps1`, que verifica su tamaño y su hash SHA-256:

| Arquitectura | Archivo | Tamaño | SHA-256 |
|---|---|---:|---|
| x64 | `tightvnc-2.8.90-gpl-setup-64bit.msi` | 2.543.616 | `9693CB4CDC2E7E482E8E3F5890415B57EF87DE3369FA5C337DB516218A1170CE` |
| x86 | `tightvnc-2.8.90-gpl-setup-32bit.msi` | 2.113.536 | `669BCF5796C2655FB1A8B556CAD8DECF25D3008A650EF495B1ED3882AA918211` |

### Cumplimiento de la GPLv2

La GPLv2 exige que los destinatarios puedan obtener el código fuente correspondiente.
CyberManager cumple con este requisito de la siguiente forma:

1. Los MSI distribuidos son los binarios oficiales de TightVNC 2.8.90, sin cambios.
2. El código fuente oficial está disponible en el enlace indicado arriba.
3. Para adjuntar una copia del código fuente a una release, ejecute:

   ```powershell
   .\scripts\download-tightvnc.ps1 -IncludeSource
   ```

   El archivo `installer\vnc\tightvnc-2.8.90-src-gpl.zip` se puede adjuntar como
   activo de la release. También puede solicitarse por escrito al mantenedor del
   proyecto a través de la página de incidencias de GitHub.

### Seguridad y configuración

- TightVNC usa autenticación VNC clásica: la contraseña se limita a **8 caracteres**
  y el cifrado de la sesión es débil. No exponga el puerto VNC a Internet.
- El instalador de estación crea la regla de Firewall `CyberManager VNC (TightVNC)`
  limitada a los perfiles **Dominio** y **Privado** y a la **subred local**.
- El puerto RFB predeterminado es **5900** y se puede cambiar durante la instalación
  (rango 1024–65535). El acceso directo **Configurar servidor VNC** actualiza la regla
  de Firewall cuando cambie el puerto.
- La desinstalación solo quita TightVNC si lo instaló CyberManager. Si el equipo ya
  tenía TightVNC o si el otro componente de CyberManager todavía lo usa, se conserva.
  El marcador compartido es `%ProgramData%\CyberManager\vnc-componentes.txt`.

## Marcas y créditos

TightVNC es una marca de sus respectivos titulares. Este proyecto no está afiliado a
GlavSoft LLC ni al proyecto TightVNC. Las demás marcas pertenecen a sus propietarios.
