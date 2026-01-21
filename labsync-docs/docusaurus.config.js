// @ts-check
// `@type` JSDoc annotations allow editor autocompletion and type checking
// (when paired with `@ts-check`).
// There are various equivalent ways to declare your Docusaurus config.
// See: https://docusaurus.io/docs/api/docusaurus-config

import {themes as prismThemes} from 'prism-react-renderer';

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'LabSync',
  tagline: 'Remote Monitoring & Management dla heterogenicznych środowisk IT',
  favicon: 'img/favicon.ico',

  // Future flags, see https://docusaurus.io/docs/api/docusaurus-config#future
  future: {
    v4: true, // Improve compatibility with the upcoming Docusaurus v4
  },

  // Set the production url of your site here
  // For GitHub Pages this is typically 'https://<org>.github.io'
  url: 'https://labsyncpl.github.io',
  // Set the /<baseUrl>/ pathname under which your site is served
  // For GitHub Pages project 'LabSync' it should be '/LabSync/'
  baseUrl: '/LabSync/',

  // GitHub pages deployment config.
  // Repo: https://github.com/LabSyncPL/LabSync
  organizationName: 'LabSyncPL', // Your GitHub org/user name.
  projectName: 'LabSync', // Your repo name.

  onBrokenLinks: 'throw',

  // Even if you don't use internationalization, you can use this field to set
  // useful metadata like html lang. For example, if your site is Chinese, you
  // may want to replace "en" with "zh-Hans".
  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  markdown: {
    mermaid: true,
  },

  themes: ['@docusaurus/theme-mermaid'],

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: './sidebars.js',
          editUrl: undefined,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      // Replace with your project's social card
      image: 'img/docusaurus-social-card.jpg',
      colorMode: {
        respectPrefersColorScheme: true,
      },
      navbar: {
        title: 'LabSync Docs',
        logo: {
          alt: 'LabSync Logo',
          src: 'img/logo.svg',
        },
        items: [
          {
            type: 'docSidebar',
            sidebarId: 'tutorialSidebar',
            position: 'left',
            label: 'Dokumentacja',
          },
          {
            to: '/docs/labsync-harmonogram',
            label: 'Harmonogram',
            position: 'left',
          },
          {
            to: '/docs/labsync-status',
            label: 'Status',
            position: 'left',
          },
          {
            to: 'makieta',
            label: 'Makieta',
            position: 'left',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Dokumentacja',
            items: [
              {
                label: 'Wprowadzenie',
                to: '/docs/intro',
              },
              {
                label: 'Harmonogram i status',
                to: '/docs/labsync-harmonogram',
              },
            ],
          },
        ],
        copyright: `Copyright © ${new Date().getFullYear()} LabSync.`,
      },
      prism: {
        theme: prismThemes.github,
        darkTheme: prismThemes.dracula,
      },
    }),
};

export default config;
