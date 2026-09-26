import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'BaGetter',
  tagline: 'A lightweight, self-hosted NuGet and symbol server',
  favicon: 'img/favicon.svg',

  url: 'https://letreset.github.io',
  baseUrl: '/BaGetter/',

  organizationName: 'letreset',
  projectName: 'BaGetter',

  // Deployed by .github/workflows/docs.yml (GitHub Pages via Actions).
  trailingSlash: false,

  onBrokenLinks: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },
  onBrokenAnchors: 'throw',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  themes: [
    [
      require.resolve('@easyops-cn/docusaurus-search-local'),
      {
        hashed: true,
        indexBlog: false,
        docsRouteBasePath: '/docs',
        highlightSearchTermsOnTargetPage: true,
      },
    ],
  ],

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          editUrl: 'https://github.com/letreset/BaGetter/tree/main/docs/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    image: 'img/social-preview.png',
    colorMode: {
      defaultMode: 'light',
      disableSwitch: false,
      respectPrefersColorScheme: true,
    },
    announcementBar: {
      id: 'release-2-0-0',
      content: '📦 <b><a target="_blank" rel="noopener" href="https://github.com/letreset/BaGetter/releases/tag/v2.0.0">BaGetter 2.0.0</a> is out</b>, with multiple feeds, user accounts and Entra ID sign-in. <a href="/BaGetter/docs/upgrading">Upgrading from 1.x?</a>',
      isCloseable: true,
    },
    docs: {
      sidebar: {
        hideable: true,
        autoCollapseCategories: true,
      },
    },
    navbar: {
      title: 'BaGetter',
      logo: {
        alt: 'BaGetter logo',
        src: 'img/logo.svg',
      },
      style: 'dark',
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'tutorialSidebar',
          position: 'left',
          label: 'Documentation',
        },
        {
          href: 'https://github.com/letreset/BaGetter/releases',
          label: 'Releases',
          position: 'left',
        },
        {
          href: 'https://hub.docker.com/r/letreset/bagetter',
          'aria-label': 'Docker Hub',
          className: 'header-docker-link',
          position: 'right',
        },
        {
          href: 'https://github.com/letreset/BaGetter',
          'aria-label': 'GitHub repository',
          className: 'header-github-link',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'light',
      links: [
        {
          title: 'Documentation',
          items: [
            {label: 'Get started', to: '/docs'},
            {label: 'Docker', to: '/docs/Installation/docker'},
            {label: 'Kubernetes', to: '/docs/Installation/kubernetes'},
            {label: 'Configuration', to: '/docs/configuration'},
          ],
        },
        {
          title: 'Community',
          items: [
            {label: 'Issues', href: 'https://github.com/letreset/BaGetter/issues'},
            {label: 'Contributing', href: 'https://github.com/letreset/BaGetter/blob/main/CONTRIBUTING.md'},
          ],
        },
        {
          title: 'More',
          items: [
            {label: 'GitHub', href: 'https://github.com/letreset/BaGetter'},
            {label: 'Releases', href: 'https://github.com/letreset/BaGetter/releases'},
            {label: 'Docker Hub', href: 'https://hub.docker.com/r/letreset/bagetter'},
          ],
        },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} BaGetter contributors. Built with <a href="https://docusaurus.io">Docusaurus</a>.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.vsDark,
      additionalLanguages: ['csharp', 'json', 'powershell', 'bash', 'yaml', 'docker', 'diff', 'ini'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
