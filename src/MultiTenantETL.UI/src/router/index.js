import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      redirect: '/dashboard'
    },
    {
      path: '/dashboard',
      name: 'dashboard',
      component: () => import('@/views/DashboardView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/login',
      name: 'login',
      component: () => import('@/views/LoginView.vue'),
      meta: { guest: true }
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/views/RegisterView.vue'),
      meta: { guest: true }
    },
    {
      path: '/forgot-password',
      name: 'forgot-password',
      component: () => import('@/views/ForgotPasswordView.vue'),
      meta: { guest: true }
    },
    {
      path: '/reset-password',
      name: 'reset-password',
      component: () => import('@/views/ResetPasswordView.vue'),
      meta: { guest: true }
    },
    {
      path: '/auth/reset-password',
      component: () => import('@/views/ResetPasswordView.vue'),
      meta: { guest: true }
    },
    {
      path: '/auth/confirm-email',
      name: 'confirm-email',
      component: () => import('@/views/ConfirmEmailView.vue'),
      meta: { public: true } // Allow both authenticated and unauthenticated users
    },
    {
      path: '/auth/callback',
      name: 'auth-callback',
      component: () => import('@/views/AuthCallbackView.vue'),
      meta: { guest: true }
    },
    {
      path: '/pipelines',
      name: 'pipelines',
      component: () => import('@/views/PipelinesView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/pipelines/new',
      name: 'pipeline-create',
      component: () => import('@/views/PipelineFormView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/pipelines/:id/edit',
      name: 'pipeline-edit',
      component: () => import('@/views/PipelineFormView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/pipelines/:id',
      name: 'pipeline-details',
      component: () => import('@/views/PipelineDetailsView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/connectors',
      name: 'connectors',
      component: () => import('@/views/ConnectorsView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/connectors/new',
      name: 'connector-create',
      component: () => import('@/views/ConnectorFormView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/connectors/:id/edit',
      name: 'connector-edit',
      component: () => import('@/views/ConnectorFormView.vue'),
      meta: { requiresAuth: true }
    },
    // Legacy routes for backward compatibility
    {
      path: '/data-sources',
      redirect: '/connectors'
    },
    {
      path: '/data-sources/new',
      redirect: '/connectors/new'
    },
    {
      path: '/data-sources/:id/edit',
      redirect: to => `/connectors/${to.params.id}/edit`
    },
    {
      path: '/schedules',
      name: 'schedules',
      component: () => import('@/views/SchedulesView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/tenants',
      name: 'tenants',
      component: () => import('@/views/TenantsView.vue'),
      meta: { requiresAuth: true, requiresAdmin: true }
    },
    {
      path: '/executions',
      name: 'executions',
      component: () => import('@/views/ExecutionsView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/settings',
      name: 'settings',
      component: () => import('@/views/SettingsView.vue'),
      meta: { requiresAuth: true }
    },
    {
      path: '/users',
      name: 'users',
      component: () => import('@/views/UsersView.vue'),
      meta: { requiresAuth: true, requiresAdmin: true }
    },
    {
      path: '/audit-logs',
      name: 'audit-logs',
      component: () => import('@/views/AuditLogsView.vue'),
      meta: { requiresAuth: true, requiresAdmin: true }
    }
  ]
})

router.beforeEach((to, from, next) => {
  const authStore = useAuthStore()
  const requiresAuth = to.matched.some(record => record.meta.requiresAuth)
  const requiresAdmin = to.matched.some(record => record.meta.requiresAdmin)
  const requiresSuperAdmin = to.matched.some(record => record.meta.requiresSuperAdmin)
  const isGuestRoute = to.matched.some(record => record.meta.guest)
  const isPublicRoute = to.matched.some(record => record.meta.public)

  // Show loading indicator for route changes (only if navigating from another route)
  if (from.name) {
    window.dispatchEvent(new CustomEvent('route-loading-start'))
  }

  // Public routes are accessible to everyone (authenticated or not)
  if (isPublicRoute) {
    next()
    return
  }

  // Authentication checks with redirect loop prevention
  if (requiresAuth && !authStore.isAuthenticated) {
    window.dispatchEvent(new CustomEvent('route-loading-end'))
    // Prevent infinite redirect loop
    if (to.path !== '/login') {
      next('/login')
    } else {
      next()
    }
  } else if (requiresSuperAdmin && (!authStore.user || authStore.user.role !== 'SuperAdmin')) {
    window.dispatchEvent(new CustomEvent('route-loading-end'))
    // Prevent infinite redirect loop
    if (to.path !== '/dashboard') {
      next('/dashboard')
    } else {
      next()
    }
  } else if (requiresAdmin && !authStore.isAdmin) {
    window.dispatchEvent(new CustomEvent('route-loading-end'))
    // Prevent infinite redirect loop
    if (to.path !== '/dashboard') {
      next('/dashboard')
    } else {
      next()
    }
  } else if (isGuestRoute && authStore.isAuthenticated) {
    window.dispatchEvent(new CustomEvent('route-loading-end'))
    // Prevent infinite redirect loop
    if (to.path !== '/dashboard') {
      next('/dashboard')
    } else {
      next()
    }
  } else {
    next()
  }
})

router.afterEach(() => {
  // Hide loading indicator after navigation completes
  // Use nextTick to ensure DOM has updated
  window.dispatchEvent(new CustomEvent('route-loading-end'))
})

export default router
