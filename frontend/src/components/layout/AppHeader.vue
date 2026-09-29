<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import Brand from './Brand.vue'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const menuOpen = ref(false)

const navLinks = [
  { to: '/jobs', label: 'Find jobs' },
  { to: '/blog', label: 'Blog' },
  { to: '/faq', label: 'FAQ' },
]

const initials = computed(() =>
  (auth.user?.fullName ?? '')
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join(''),
)

const roleLabel = computed(() => {
  const role = auth.roles[0]
  return role ? role.charAt(0).toUpperCase() + role.slice(1) : ''
})

function isActive(to: string): boolean {
  return route.path === to || route.path.startsWith(`${to}/`)
}

function closeMenu(): void {
  menuOpen.value = false
}

async function signOut(): Promise<void> {
  closeMenu()
  await auth.logout()
  await router.push({ name: 'home' })
}
</script>

<template>
  <header class="sticky top-0 z-50 bg-white/80 backdrop-blur-xl backdrop-saturate-150">
    <div class="shell flex h-12 items-center gap-8">
      <Brand @click="closeMenu" />

      <nav class="hidden items-center gap-7 md:flex" aria-label="Main">
        <RouterLink
          v-for="link in navLinks"
          :key="link.to"
          :to="link.to"
          class="text-[13px] text-slate-800 transition-colors hover:text-brand-600"
          :class="isActive(link.to) ? 'text-brand-600' : ''"
          :aria-current="isActive(link.to) ? 'page' : undefined"
        >
          {{ link.label }}
        </RouterLink>
        <RouterLink
          v-if="auth.isEmployer || auth.isAdmin"
          :to="{ name: 'dashboard' }"
          class="text-[13px] text-slate-800 transition-colors hover:text-brand-600"
        >
          Dashboard
        </RouterLink>
        <RouterLink
          v-if="auth.isAdmin"
          :to="{ name: 'admin' }"
          class="text-[13px] text-slate-800 transition-colors hover:text-brand-600"
        >
          Admin
        </RouterLink>
      </nav>

      <div class="ml-auto hidden items-center gap-5 md:flex">
        <template v-if="!auth.isAuthenticated">
          <RouterLink :to="{ name: 'login' }" class="text-[13px] text-brand-600 hover:underline">Log in</RouterLink>
          <RouterLink :to="{ name: 'register' }" class="btn-primary btn-sm">Sign up</RouterLink>
        </template>

        <template v-else>
          <RouterLink v-if="auth.isWorker" :to="{ name: 'applications' }" class="text-[13px] text-slate-800 hover:text-brand-600">
            Applications
          </RouterLink>
          <RouterLink v-if="auth.isWorker" :to="{ name: 'profile' }" class="text-[13px] text-slate-800 hover:text-brand-600">
            Profile
          </RouterLink>
          <span class="inline-flex items-center gap-2">
            <span
              class="grid h-6 w-6 place-items-center rounded-full bg-slate-900 text-[10px] font-semibold text-white"
              aria-hidden="true"
            >
              {{ initials || '·' }}
            </span>
            <span class="leading-tight">
              <span class="block max-w-[11ch] truncate text-[12px] font-medium text-slate-900">
                {{ auth.user?.fullName }}
              </span>
              <span v-if="roleLabel" class="block text-[10px] text-slate-500">{{ roleLabel }}</span>
            </span>
          </span>
          <button class="text-[13px] text-slate-800 hover:text-brand-600" type="button" @click="signOut">
            Log out
          </button>
        </template>
      </div>

      <button
        class="ml-auto text-[13px] text-slate-900 md:hidden"
        type="button"
        :aria-expanded="menuOpen"
        aria-controls="mobile-menu"
        @click="menuOpen = !menuOpen"
      >
        {{ menuOpen ? 'Close' : 'Menu' }}
      </button>
    </div>

    <!-- Mobile menu -->
    <div v-if="menuOpen" id="mobile-menu" class="border-t border-slate-200 bg-white md:hidden">
      <nav class="shell flex flex-col gap-4 py-5" aria-label="Mobile">
        <RouterLink
          v-for="link in navLinks"
          :key="link.to"
          :to="link.to"
          class="text-[17px] text-slate-900"
          @click="closeMenu"
        >
          {{ link.label }}
        </RouterLink>
        <RouterLink
          v-if="auth.isAuthenticated"
          :to="{ name: 'dashboard' }"
          class="text-[17px] text-slate-900"
          @click="closeMenu"
        >
          Dashboard
        </RouterLink>
        <RouterLink
          v-if="auth.isWorker"
          :to="{ name: 'applications' }"
          class="text-[17px] text-slate-900"
          @click="closeMenu"
        >
          Applications
        </RouterLink>
        <RouterLink
          v-if="auth.isWorker"
          :to="{ name: 'profile' }"
          class="text-[17px] text-slate-900"
          @click="closeMenu"
        >
          Profile
        </RouterLink>
        <RouterLink
          v-if="auth.isAdmin"
          :to="{ name: 'admin' }"
          class="text-[17px] text-slate-900"
          @click="closeMenu"
        >
          Admin
        </RouterLink>

        <div class="mt-2 border-t border-slate-200 pt-4">
          <template v-if="!auth.isAuthenticated">
            <div class="flex gap-3">
              <RouterLink :to="{ name: 'login' }" class="btn-secondary flex-1" @click="closeMenu">Log in</RouterLink>
              <RouterLink :to="{ name: 'register' }" class="btn-primary flex-1" @click="closeMenu">Sign up</RouterLink>
            </div>
          </template>
          <template v-else>
            <p class="text-[13px] text-slate-500">
              Signed in as <strong class="font-medium text-slate-900">{{ auth.user?.fullName }}</strong>
            </p>
            <button class="btn-secondary mt-2 w-full" type="button" @click="signOut">Log out</button>
          </template>
        </div>
      </nav>
    </div>
  </header>
</template>
