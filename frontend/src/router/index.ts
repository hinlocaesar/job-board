import { createMemoryHistory, createRouter as createVueRouter, createWebHistory, type Router } from 'vue-router'
import type { RouteRecordRaw } from 'vue-router'
import { session } from '../api/session'
import type { Role } from '../api/types'

export const routes: RouteRecordRaw[] = [
  { path: '/', name: 'home', component: () => import('../pages/HomePage.vue') },
  { path: '/jobs', name: 'jobs', component: () => import('../pages/JobsPage.vue') },
  { path: '/jobs/new', name: 'job-new', component: () => import('../pages/JobPostPage.vue'), meta: { auth: true, roles: ['employer'] } },
  { path: '/jobs/:slug', name: 'job-detail', component: () => import('../pages/JobDetailPage.vue') },
  { path: '/job/:id/edit', name: 'job-edit', component: () => import('../pages/JobPostPage.vue'), meta: { auth: true, roles: ['employer'] } },

  { path: '/login', name: 'login', component: () => import('../pages/LoginPage.vue'), meta: { guestOnly: true } },
  { path: '/register', name: 'register', component: () => import('../pages/RegisterPage.vue'), meta: { guestOnly: true } },
  { path: '/verify-email', name: 'verify-email', component: () => import('../pages/VerifyEmailPage.vue') },
  { path: '/forgot-password', name: 'forgot-password', component: () => import('../pages/ForgotPasswordPage.vue'), meta: { guestOnly: true } },
  { path: '/reset-password', name: 'reset-password', component: () => import('../pages/ResetPasswordPage.vue'), meta: { guestOnly: true } },

  { path: '/account', name: 'account', component: () => import('../pages/AccountPage.vue'), meta: { auth: true } },
  { path: '/profile', name: 'profile', component: () => import('../pages/ProfileEditPage.vue'), meta: { auth: true, roles: ['worker'] } },
  { path: '/u/:slug', name: 'worker-profile', component: () => import('../pages/WorkerProfilePage.vue') },
  { path: '/applications', name: 'applications', component: () => import('../pages/ApplicationsPage.vue'), meta: { auth: true, roles: ['worker'] } },

  { path: '/dashboard', name: 'dashboard', component: () => import('../pages/EmployerDashboardPage.vue'), meta: { auth: true, roles: ['employer'] } },
  { path: '/employer-profile', name: 'employer-profile', component: () => import('../pages/EmployerProfilePage.vue'), meta: { auth: true, roles: ['employer'] } },
  { path: '/applicants/:jobId', name: 'applicants', component: () => import('../pages/ApplicantsPage.vue'), meta: { auth: true, roles: ['employer'] } },

  { path: '/admin', name: 'admin', component: () => import('../pages/AdminPage.vue'), meta: { auth: true, roles: ['admin'] } },

  // Umbraco-backed marketing content
  { path: '/blog', name: 'blog', component: () => import('../pages/BlogListPage.vue') },
  { path: '/blog/:slug', name: 'blog-post', component: () => import('../pages/BlogPostPage.vue') },
  { path: '/faq', name: 'faq', component: () => import('../pages/FaqPage.vue') },
  { path: '/:slug(hire-.*)', name: 'landing', component: () => import('../pages/LandingPage.vue') },

  { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('../pages/NotFoundPage.vue') },
]

/** Routes that must render without a session (also during prerender). */
const PUBLIC_ROUTES = new Set([
  'home', 'jobs', 'job-detail', 'login', 'register', 'verify-email', 'forgot-password',
  'reset-password', 'blog', 'blog-post', 'faq', 'landing', 'not-found', 'worker-profile',
])

/**
 * One router per app instance: the prerender creates a fresh app (and router)
 * for every URL, which would otherwise leak previous apps inside the singleton.
 */
export function createAppRouter(): Router {
  const router = createVueRouter({
    // Memory history during prerender (Node), browser history in the app.
    history: import.meta.env.SSR ? createMemoryHistory() : createWebHistory(),
    routes,
    scrollBehavior(to, from, saved) {
      if (saved) return saved
      if (to.hash) return { el: to.hash, behavior: 'smooth' }
      if (to.path !== from.path) return { top: 0 }
      return undefined
    },
  })

  router.beforeEach((to) => {
    const stored = session.load()
    const roles = (stored?.user?.roles ?? []) as Role[]
    const meta = to.meta as { auth?: boolean; roles?: Role[]; guestOnly?: boolean }

    if (import.meta.env.SSR && PUBLIC_ROUTES.has(String(to.name))) return true

    if (meta.guestOnly && stored) {
      return { name: roles.includes('admin') ? 'admin' : roles.includes('employer') ? 'dashboard' : 'applications' }
    }

    if (meta.auth && !stored) {
      return { name: 'login', query: { redirect: to.fullPath } }
    }

    if (meta.roles && meta.roles.length > 0) {
      const allowed = meta.roles.some((role) => roles.includes(role))
      if (!allowed) return { name: roles.includes('admin') ? 'admin' : 'home' }
    }

    return true
  })

  return router
}
