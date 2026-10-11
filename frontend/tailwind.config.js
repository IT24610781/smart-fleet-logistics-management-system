/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './index.html',
    './src/**/*.{js,jsx}',
  ],
  theme: {
    extend: {
  "colors": {
    "surface": "rgb(var(--sf-surface) / <alpha-value>)",
    "on-error-container": "rgb(var(--sf-on-error-container) / <alpha-value>)",
    "on-surface": "rgb(var(--sf-on-surface) / <alpha-value>)",
    "outline-variant": "rgb(var(--sf-outline-variant) / <alpha-value>)",
    "secondary-container": "rgb(var(--sf-secondary-container) / <alpha-value>)",
    "surface-variant": "rgb(var(--sf-surface-variant) / <alpha-value>)",
    "on-primary": "rgb(var(--sf-on-primary) / <alpha-value>)",
    "primary": "rgb(var(--sf-primary) / <alpha-value>)",
    "primary-fixed": "rgb(var(--sf-primary-fixed) / <alpha-value>)",
    "on-secondary-fixed": "rgb(var(--sf-on-secondary-fixed) / <alpha-value>)",
    "error": "rgb(var(--sf-error) / <alpha-value>)",
    "on-secondary": "rgb(var(--sf-on-secondary) / <alpha-value>)",
    "tertiary-container": "rgb(var(--sf-tertiary-container) / <alpha-value>)",
    "surface-bright": "rgb(var(--sf-surface-bright) / <alpha-value>)",
    "secondary": "rgb(var(--sf-secondary) / <alpha-value>)",
    "on-primary-fixed": "rgb(var(--sf-on-primary-fixed) / <alpha-value>)",
    "on-background": "rgb(var(--sf-on-background) / <alpha-value>)",
    "surface-container-lowest": "rgb(var(--sf-surface-container-lowest) / <alpha-value>)",
    "surface-container-highest": "rgb(var(--sf-surface-container-highest) / <alpha-value>)",
    "tertiary-fixed-dim": "rgb(var(--sf-tertiary-fixed-dim) / <alpha-value>)",
    "inverse-surface": "rgb(var(--sf-inverse-surface) / <alpha-value>)",
    "on-tertiary-fixed-variant": "rgb(var(--sf-on-tertiary-fixed-variant) / <alpha-value>)",
    "surface-container-low": "rgb(var(--sf-surface-container-low) / <alpha-value>)",
    "secondary-fixed-dim": "rgb(var(--sf-secondary-fixed-dim) / <alpha-value>)",
    "error-container": "rgb(var(--sf-error-container) / <alpha-value>)",
    "on-primary-container": "rgb(var(--sf-on-primary-container) / <alpha-value>)",
    "on-secondary-container": "rgb(var(--sf-on-secondary-container) / <alpha-value>)",
    "tertiary": "rgb(var(--sf-tertiary) / <alpha-value>)",
    "on-primary-fixed-variant": "rgb(var(--sf-on-primary-fixed-variant) / <alpha-value>)",
    "on-tertiary": "rgb(var(--sf-on-tertiary) / <alpha-value>)",
    "secondary-fixed": "rgb(var(--sf-secondary-fixed) / <alpha-value>)",
    "surface-container-high": "rgb(var(--sf-surface-container-high) / <alpha-value>)",
    "tertiary-fixed": "rgb(var(--sf-tertiary-fixed) / <alpha-value>)",
    "surface-tint": "rgb(var(--sf-surface-tint) / <alpha-value>)",
    "outline": "rgb(var(--sf-outline) / <alpha-value>)",
    "primary-container": "rgb(var(--sf-primary-container) / <alpha-value>)",
    "surface-container": "rgb(var(--sf-surface-container) / <alpha-value>)",
    "on-surface-variant": "rgb(var(--sf-on-surface-variant) / <alpha-value>)",
    "surface-dim": "rgb(var(--sf-surface-dim) / <alpha-value>)",
    "on-tertiary-fixed": "rgb(var(--sf-on-tertiary-fixed) / <alpha-value>)",
    "background": "rgb(var(--sf-background) / <alpha-value>)",
    "inverse-primary": "rgb(var(--sf-inverse-primary) / <alpha-value>)",
    "on-error": "rgb(var(--sf-on-error) / <alpha-value>)",
    "on-secondary-fixed-variant": "rgb(var(--sf-on-secondary-fixed-variant) / <alpha-value>)",
    "on-tertiary-container": "rgb(var(--sf-on-tertiary-container) / <alpha-value>)",
    "inverse-on-surface": "rgb(var(--sf-inverse-on-surface) / <alpha-value>)",
    "primary-fixed-dim": "rgb(var(--sf-primary-fixed-dim) / <alpha-value>)"
},
  "borderRadius": {
    "DEFAULT": "0.125rem",
    "lg": "0.25rem",
    "xl": "0.5rem",
    "full": "0.75rem"
  },
  "spacing": {
    "space-base": "1rem",
    "gutter-tablet": "1.5rem",
    "space-2xl": "3rem",
    "space-2xs": "0.125rem",
    "gutter-desktop": "1.5rem",
    "gutter-mobile": "1rem",
    "space-md": "0.75rem",
    "space-xl": "2rem",
    "sidebar-collapsed": "4.5rem",
    "space-lg": "1.5rem",
    "space-xs": "0.25rem",
    "sidebar-expanded": "16rem",
    "space-sm": "0.5rem"
  },
  "fontFamily": {
    "headline-lg": [
      "Plus Jakarta Sans"
    ],
    "body-sm": [
      "Inter"
    ],
    "body-md": [
      "Inter"
    ],
    "title-md": [
      "Inter"
    ],
    "data-mono": [
      "Inter"
    ],
    "headline-sm": [
      "Plus Jakarta Sans"
    ],
    "headline-xl-mobile": [
      "Plus Jakarta Sans"
    ],
    "label-sm": [
      "Inter"
    ],
    "body-lg": [
      "Inter"
    ],
    "label-md": [
      "Inter"
    ],
    "headline-xl": [
      "Plus Jakarta Sans"
    ]
  },
  "fontSize": {
    "headline-lg": [
      "24px",
      {
        "lineHeight": "32px",
        "letterSpacing": "-0.015em",
        "fontWeight": "600"
      }
    ],
    "body-sm": [
      "13px",
      {
        "lineHeight": "18px",
        "fontWeight": "400"
      }
    ],
    "body-md": [
      "14px",
      {
        "lineHeight": "20px",
        "fontWeight": "400"
      }
    ],
    "title-md": [
      "16px",
      {
        "lineHeight": "24px",
        "letterSpacing": "-0.005em",
        "fontWeight": "600"
      }
    ],
    "data-mono": [
      "13px",
      {
        "lineHeight": "18px",
        "letterSpacing": "-0.01em",
        "fontWeight": "500"
      }
    ],
    "headline-sm": [
      "18px",
      {
        "lineHeight": "26px",
        "letterSpacing": "-0.01em",
        "fontWeight": "600"
      }
    ],
    "headline-xl-mobile": [
      "28px",
      {
        "lineHeight": "36px",
        "letterSpacing": "-0.02em",
        "fontWeight": "700"
      }
    ],
    "label-sm": [
      "11px",
      {
        "lineHeight": "14px",
        "letterSpacing": "0.04em",
        "fontWeight": "600"
      }
    ],
    "body-lg": [
      "16px",
      {
        "lineHeight": "24px",
        "fontWeight": "400"
      }
    ],
    "label-md": [
      "12px",
      {
        "lineHeight": "16px",
        "letterSpacing": "0.01em",
        "fontWeight": "500"
      }
    ],
    "headline-xl": [
      "36px",
      {
        "lineHeight": "44px",
        "letterSpacing": "-0.02em",
        "fontWeight": "700"
      }
    ]
  }
},
  },
}
