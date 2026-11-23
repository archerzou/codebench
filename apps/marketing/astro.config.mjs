// @ts-check
import { defineConfig } from 'astro/config';
import tailwindcss from '@tailwindcss/vite';
import sitemap from '@astrojs/sitemap';
import mdx from '@astrojs/mdx';

// Static marketing site (guide 04, FE-13). Tailwind v4 is a Vite plugin here — never @astrojs/tailwind (v3-only,
// can't read docs/design/tokens.css). No client framework: shadcn/ui, zustand and lucide-react stay in apps/web.
export default defineConfig({
  site: process.env.SITE_URL ?? 'http://codebench.localhost',
  output: 'static',
  integrations: [sitemap(), mdx()],
  vite: {
    plugins: [tailwindcss()],
    build: { sourcemap: false },
  },
});
