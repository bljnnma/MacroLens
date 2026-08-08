/**
 * Runs before first paint, ahead of hydration.
 *
 * Note what it does NOT do: resolve "system". When the preference is system
 * (the default), no attribute is written at all and the `prefers-color-scheme`
 * media query in globals.css governs — so the common case needs zero JS at
 * paint time and can never flash.
 */
const SCRIPT = `(function(){try{var p=localStorage.getItem('theme');if(p==='light'||p==='dark'){document.documentElement.setAttribute('data-theme',p);}}catch(e){}})();`;

export function ThemeScript() {
  return <script dangerouslySetInnerHTML={{ __html: SCRIPT }} />;
}
