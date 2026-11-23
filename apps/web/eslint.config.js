// FE-12: eslint recommended + typescript-eslint strict (type-checked) + react-hooks + jsx-a11y; zero warnings.
import js from '@eslint/js'
import jsxA11y from 'eslint-plugin-jsx-a11y'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import { defineConfig, globalIgnores } from 'eslint/config'
import globals from 'globals'
import tseslint from 'typescript-eslint'

export default defineConfig([
  globalIgnores(['dist', 'coverage', 'public/config.js']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.strictTypeChecked,
      tseslint.configs.stylisticTypeChecked,
      reactHooks.configs.flat['recommended-latest'],
      reactRefresh.configs.vite,
      jsxA11y.flatConfigs.recommended,
    ],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      'no-console': ['error', { allow: ['warn', 'error'] }], // FE-14
      '@typescript-eslint/no-explicit-any': 'error', // FE-01
      '@typescript-eslint/consistent-type-definitions': ['error', 'type'], // FE-09: `type XProps`
      '@typescript-eslint/restrict-template-expressions': ['error', { allowNumber: true }],
      'no-restricted-imports': [
        'error',
        {
          paths: [
            { name: 'lucide-react', importNames: ['DynamicIcon', 'icons'], message: 'Named icon imports only (FE-17).' },
          ],
          patterns: [{ group: ['lucide-react/dynamic*'], message: 'Named icon imports only (FE-17).' }],
        },
      ],
    },
  },
  {
    // KD-07: shadcn/ui primitives keep upstream's multi-export, kebab-case files.
    files: ['src/components/ui/**/*.{ts,tsx}'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },
  {
    // Entry points and the route table aren't Fast Refresh boundaries.
    files: ['src/main.tsx', 'src/routes.tsx', '**/*.test.{ts,tsx}', 'src/test/**/*.{ts,tsx}'],
    rules: { 'react-refresh/only-export-components': 'off' },
  },
])
