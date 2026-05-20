// @ts-check

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

/**
 * Creating a sidebar enables you to:
 - create an ordered group of docs
 - render a sidebar for each doc of that group
 - provide next/previous navigation

 The sidebars can be generated from the filesystem, or explicitly defined here.

 Create as many sidebars as you want.

 @type {import('@docusaurus/plugin-content-docs').SidebarsConfig}
 */
const sidebars = {
  tutorialSidebar: [
    {
      type: "doc",
      id: "intro",
      label: "Introduction",
    },
    {
      type: "category",
      label: "Getting Started",
      items: [
        {
          type: "doc",
          id: "getting-started/quick-start",
          label: "Quick Start",
        },
        {
          type: "doc",
          id: "getting-started/installation",
          label: "Installation Guide",
        },
      ],
    },
    {
      type: "category",
      label: "Features",
      items: [
        {
          type: "doc",
          id: "features/overview",
          label: "Features Overview",
        },
        {
          type: "category",
          label: "User Guide",
          items: [
            {
              type: "doc",
              id: "user-guide/dashboard",
              label: "Dashboard",
            },
            {
              type: "doc",
              id: "user-guide/device-management",
              label: "Device Management",
            },
            {
              type: "doc",
              id: "user-guide/remote-desktop",
              label: "Remote Desktop",
            },
            {
              type: "doc",
              id: "user-guide/script-execution",
              label: "Script Execution",
            },
            {
              type: "doc",
              id: "user-guide/ssh-terminal",
              label: "SSH Terminal",
            },
            {
              type: "doc",
              id: "user-guide/scheduling",
              label: "Scheduling",
            },
          ],
        },
      ],
    },
    {
      type: "category",
      label: "Architecture",
      items: [
        {
          type: "doc",
          id: "architecture/overview",
          label: "System Architecture",
        },
      ],
    },
    {
      type: "category",
      label: "Business",
      items: [
        {
          type: "doc",
          id: "business/potential",
          label: "Business Potential",
        },
      ],
    },
    {
      type: "category",
      label: "Development",
      items: [
        {
          type: "doc",
          id: "development/modules",
          label: "Module Development",
        },
      ],
    },
    {
      type: "category",
      label: "API Reference",
      items: [
        {
          type: "doc",
          id: "api-reference/overview",
          label: "API Documentation",
        },
      ],
    },
    {
      type: "doc",
      id: "troubleshooting",
      label: "Troubleshooting",
    },
    {
      type: "doc",
      id: "labsync-harmonogram",
      label: "Roadmap",
    },
    {
      type: "doc",
      id: "labsync-status",
      label: "Status",
    },
  ],
};

export default sidebars;
