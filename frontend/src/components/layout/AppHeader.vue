<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const menuOpen = ref(false)

const navLinks = [
  { to: '/jobs', label: 'Find jobs' },
  { to: '/blog', label: 'Blog' },
  { to: '/faq', label: 'FAQ' },
]

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
  <header class="sticky top-0 z-40 border-b border-slate-200 bg-white/95 backdrop-blur">
    <div class="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4 sm:px-6">
      <RouterLink :to="{ name: 'home' }" class="flex items-center gap-2 font-semibold text-slate-900" @click="closeMenu">
        <span class="grid h-8 w-8 place-items-center rounded-lg bg-brand-600 text-sm font-bold text-white">JB</span>
        <span class="text-lg">JobBoard</span>
      </RouterLink>

      <nav class="hidden items-center gap-1 md:flex">
        <RouterLink
          v-for="link in navLinks"
          :key="link.to"
          :to="link.to"
          class="rounded-lg px-3 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 hover:text-slate-900"
          :class="{ 'bg-slate-100 text-slate-900': route.path === link.to }"
        >
          {{ link.label }}
        </RouterLink>
        <RouterLink
          v-if="auth.isEmployer || auth.isAdmin"
          :to="{ name: 'dashboard' }"
          class="rounded-lg px-3 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 hover:text-slate-900"
        >
          Employer dashboard
        </RouterLink>
        <RouterLink
          v-if="auth.isAdmin"
          :to="{ name: 'admin' }"
          class="rounded-lg px-3 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100 hover:text-slate-900"
        >
          Admin
        </RouterLink>
      </nav>

      <div class="ml-auto hidden items-center gap-2 md:flex">
        <template v-if="!auth.isAuthenticated">
          <RouterLink :to="{ name: 'login' }" class="btn-secondary">Log in</RouterLink>
          <RouterLink :to="{ name: 'register' }" class="btn-primary">Sign up</RouterLink>
        </template>
        <template v-else>
          <span class="text-sm text-slate-500">
            {{ auth.user?.fullName }}
            <span class="ml-1 rounded bg-slate-100 px-1.5 py-0.5 text-xs uppercase text-slate-500">
              {{ auth.roles[0] }}
            </span>
          </span>
          <RouterLink v-if="auth.isWorker" :to="{ name: 'applications' }" class="btn-secondary">My applications</RouterLink>
          <RouterLink v-if="auth.isWorker" :to="{ name: 'profile' }" class="btn-secondary">Profile</RouterLink>
          <button class="btn-secondary" type="button" @click="signOut">Log out</button>
        </template>
      </div>

      <button
        class="ml-auto grid h-10 w-10 place-items-center rounded-lg border border-slate-200 md:hidden"
        type="button"
        aria-label="Toggle menu"
        @click="menuOpen = !menuOpen"
      >
        <span class="text-lg">{{ menuOpen ? '×' : '☰' }}</span>
      </button>
    </div>

    <!-- Mobile menu -->
    <div v-if="menuOpen" class="border-t border-slate-200 bg-white px-4 py-3 md:hidden">
      <nav class="flex flex-col gap-1">
        <RouterLink
          v-for="link in navLinks"
          :key="link.to"
          :to="link.to"
          class="rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100"
          @click="closeMenu"
        >
          {{ link.label }}
        </RouterLink>

        <template v-if="auth.isAuthenticated">
          <RouterLink v-if="auth.isEmployer" :to="{ name: 'dashboard' }" class="rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100" @click="closeMenu">
            Employer dashboard
          </RouterLink>
          <RouterLink v-if="auth.isWorker" :to="{ name: 'profile' }" class="rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100" @click="closeMenu">
            My profile
          </RouterLink>
          <RouterLink v-if="auth.isWorker" :to="{ name: 'applications' }" class="rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100" @click="closeMenu">
            My applications
          </RouterLink>
          <RouterLink v-if="auth.isAdmin" :to="{ name: 'admin' }" class="rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100" @click="closeMenu">
            Admin
          </RouterLink>
          <button class="rounded-lg px-3 py-2 text-left text-sm font-medium text-rose-600 hover:bg-slate-100" type="button" @click="signOut">
            Log out ({{ auth.user?.fullName }})
          </button>
        </template>
        <template v-else>
          <RouterLink :to="{ name: 'login' }" class="rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100" @click="closeMenu">
            Log in
          </RouterLink>
          <RouterLink :to="{ name: 'register' }" class="rounded-lg px-3 py-2 text-sm font-medium text-slate-700 hover:bg-slate-100" @click="closeMenu">
            Sign up
          </RouterLink>
        </template>
      </nav>
    </div>
  </header>
</template>
