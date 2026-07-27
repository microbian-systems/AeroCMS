import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
  site: 'https://docs.getaerocms.net',
  integrations: [
    starlight({
      title: 'AeroCMS',
      description: 'Evidence-based documentation for the AeroCMS alpha.',
      social: [
        {
          icon: 'github',
          label: 'AeroCMS source',
          href: 'https://github.com/microbian-systems/AeroCMS',
        },
      ],
      sidebar: [
        {
          label: 'Getting started',
          items: [
            { label: 'Overview', slug: '' },
            { label: 'End-to-end tutorial', slug: 'getting-started' },
            { label: 'Configuration', slug: 'developers/configuration' },
          ],
        },
        {
          label: 'Concepts',
          items: [
            { label: 'Architecture', slug: 'concepts/architecture' },
            { label: 'Feature inventory', slug: 'concepts/feature-inventory' },
            { label: 'Sites, tenants, and cultures', slug: 'concepts/sites-tenants-cultures' },
            { label: 'Content model and hierarchy', slug: 'guides/content-modeling' },
          ],
        },
        {
          label: 'Author and manage',
          items: [
            { label: 'Pages and rendering', slug: 'guides/pages-and-rendering' },
            { label: 'Posts and documentation', slug: 'guides/posts-and-docs' },
            { label: 'Manager and member identity', slug: 'guides/identity-and-access' },
            { label: 'Themes, media, navigation', slug: 'guides/site-presentation' },
            { label: 'AI and MCP', slug: 'guides/ai-and-mcp' },
            { label: 'Commerce status', slug: 'guides/commerce' },
          ],
        },
        {
          label: 'Integrate and extend',
          items: [
            { label: 'Public content query API', slug: 'guides/public-query-api' },
            { label: 'Extension development', slug: 'developers/extensions' },
            { label: 'Runnable examples', slug: 'reference/examples' },
            { label: 'API reference', link: '/api/index.html' },
          ],
        },
        {
          label: 'Operate safely',
          items: [
            { label: 'Deployment and operations', slug: 'operations/deployment' },
            { label: 'Security hardening', slug: 'operations/security' },
            { label: 'Troubleshooting', slug: 'operations/troubleshooting' },
          ],
        },
        {
          label: 'Reference',
          items: [
            { label: 'Feature status', slug: 'reference/feature-status' },
            { label: 'Documentation coverage', slug: 'reference/coverage' },
            { label: 'Glossary', slug: 'reference/glossary' },
            { label: 'AI ingestion', slug: 'reference/ai-ingestion' },
          ],
        },
      ],
      customCss: ['./src/styles/custom.css'],
    }),
  ],
  outDir: 'dist',
});
