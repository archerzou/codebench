import { setupServer } from 'msw/node'

export const API = 'http://api.test' // = VITE_API_URL in vite.config.ts `test.env`

/** MSW server; each test installs the handlers it needs with `server.use(...)`. */
export const server = setupServer()
