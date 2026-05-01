// Umbraco Documentation - App.js
// All interactive functionality for the documentation site

document.addEventListener('DOMContentLoaded', function() {
  // ===== Sidebar Toggle (expand/collapse groups) =====
  const sidebarToggles = document.querySelectorAll('.sidebar-toggle');
  sidebarToggles.forEach(function(toggle) {
    toggle.addEventListener('click', function(e) {
      // Don't trigger if clicking a link inside
      if (e.target.tagName === 'A') return;
      
      const group = toggle.closest('.sidebar-group') || toggle.parentElement;
      const children = group.querySelector('.sidebar-children');
      const chevron = toggle.querySelector('.ph-caret-right, .ph-caret-down');
      
      if (children) {
        children.classList.toggle('hidden');
        if (chevron) {
          chevron.classList.toggle('chevron-rotate');
        }
      }
    });
  });

  // ===== Dropdown Menus =====
  const dropdownContainers = document.querySelectorAll('[data-dropdown]');
  
  dropdownContainers.forEach(function(container) {
    const trigger = container.querySelector('button, .nav-tab, .ask-btn');
    const menu = container.querySelector('.dropdown-menu');
    
    if (!trigger || !menu) return;
    
    trigger.addEventListener('click', function(e) {
      e.stopPropagation();
      // Close all other dropdowns
      dropdownContainers.forEach(function(other) {
        if (other !== container) {
          const otherMenu = other.querySelector('.dropdown-menu');
          if (otherMenu) otherMenu.classList.remove('show');
        }
      });
      menu.classList.toggle('show');
    });
  });
  
  // Close dropdowns on outside click
  document.addEventListener('click', function() {
    dropdownContainers.forEach(function(container) {
      const menu = container.querySelector('.dropdown-menu');
      if (menu) menu.classList.remove('show');
    });
  });

  // ===== Search Overlay =====
  const searchBtn = document.getElementById('searchBtn');
  const searchOverlay = document.getElementById('searchOverlay');
  
  if (searchBtn && searchOverlay) {
    searchBtn.addEventListener('click', function() {
      searchOverlay.classList.remove('hidden');
      const input = searchOverlay.querySelector('input');
      if (input) {
        setTimeout(function() { input.focus(); }, 100);
      }
    });
    
    // Close on ESC or clicking outside
    searchOverlay.addEventListener('click', function(e) {
      if (e.target === searchOverlay) {
        searchOverlay.classList.add('hidden');
      }
    });
    
    document.addEventListener('keydown', function(e) {
      if (e.key === 'Escape' && !searchOverlay.classList.contains('hidden')) {
        searchOverlay.classList.add('hidden');
      }
      // Ctrl+K to open search
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
        e.preventDefault();
        searchOverlay.classList.remove('hidden');
        const input = searchOverlay.querySelector('input');
        if (input) {
          setTimeout(function() { input.focus(); }, 100);
        }
      }
    });
  }

  // ===== Mobile Menu =====
  const mobileMenuBtn = document.getElementById('mobileMenuBtn');
  const mobileMenu = document.getElementById('mobileMenu');
  
  if (mobileMenuBtn && mobileMenu) {
    mobileMenuBtn.addEventListener('click', function() {
      mobileMenu.classList.toggle('hidden');
    });
    
    mobileMenu.addEventListener('click', function(e) {
      if (e.target === mobileMenu) {
        mobileMenu.classList.add('hidden');
      }
    });
  }

  // ===== GitBook Assistant Chat Panel =====
  const openChatPanel = document.getElementById('openChatPanel');
  const closeChatPanel = document.getElementById('closeChatPanel');
  const chatPanel = document.getElementById('chatPanel');
  
  if (openChatPanel && chatPanel) {
    openChatPanel.addEventListener('click', function(e) {
      e.preventDefault();
      chatPanel.classList.add('open');
      // Close the dropdown that opened this
      dropdownContainers.forEach(function(container) {
        const menu = container.querySelector('.dropdown-menu');
        if (menu) menu.classList.remove('show');
      });
    });
  }
  
  if (closeChatPanel && chatPanel) {
    closeChatPanel.addEventListener('click', function() {
      chatPanel.classList.remove('open');
    });
  }

  // ===== Emoji Feedback =====
  const emojiBtns = document.querySelectorAll('.emoji-btn');
  emojiBtns.forEach(function(btn) {
    btn.addEventListener('click', function() {
      // Remove selected from siblings
      const siblings = btn.parentElement.querySelectorAll('.emoji-btn');
      siblings.forEach(function(s) { s.classList.remove('selected'); });
      // Add selected to clicked
      btn.classList.add('selected');
    });
  });

  // ===== "On This Page" Scroll Spy =====
  const onPageLinks = document.querySelectorAll('.on-page-link');
  const sections = document.querySelectorAll('section[id], div[id]');
  
  if (onPageLinks.length > 0 && sections.length > 0) {
    const observerOptions = {
      root: null,
      rootMargin: '-80px 0px -60% 0px',
      threshold: 0
    };
    
    const observer = new IntersectionObserver(function(entries) {
      entries.forEach(function(entry) {
        if (entry.isIntersecting) {
          const id = entry.target.getAttribute('id');
          onPageLinks.forEach(function(link) {
            link.classList.remove('active');
            if (link.getAttribute('href') === '#' + id) {
              link.classList.add('active');
            }
          });
        }
      });
    }, observerOptions);
    
    sections.forEach(function(section) {
      observer.observe(section);
    });
  }

  // ===== Search Filter Toggle =====
  const searchFilterBtn = document.getElementById('searchFilterBtn');
  if (searchFilterBtn) {
    let filterIndex = 0;
    const filters = ['All docs', 'CMS'];
    searchFilterBtn.addEventListener('click', function() {
      filterIndex = (filterIndex + 1) % filters.length;
      const span = searchFilterBtn.querySelector('span');
      if (span) span.textContent = 'Filter: ' + filters[filterIndex];
    });
  }

  // ===== Theme Toggle (visual only - cycles through states) =====
  const themeButtons = document.querySelectorAll('.fixed.bottom-4 button');
  themeButtons.forEach(function(btn, index) {
    btn.addEventListener('click', function() {
      // Reset all
      themeButtons.forEach(function(b) {
        b.classList.remove('text-primary');
        b.classList.add('text-txt-muted');
      });
      // Set active
      btn.classList.remove('text-txt-muted');
      btn.classList.add('text-primary');
    });
  });

  // ===== Smooth scroll for anchor links =====
  document.querySelectorAll('a[href^="#"]').forEach(function(anchor) {
    anchor.addEventListener('click', function(e) {
      const href = this.getAttribute('href');
      if (href === '#') return; // Skip empty anchors
      
      const target = document.querySelector(href);
      if (target) {
        e.preventDefault();
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    });
  });
});
