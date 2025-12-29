/**
 * TRAVIL - Site JavaScript
 * Handles common interactions and functionality
 */

document.addEventListener('DOMContentLoaded', function() {
    
    // ========================================
    // NAVBAR SCROLL EFFECT
    // ========================================
    const navbar = document.getElementById('navbar');
    
    if (navbar && !navbar.classList.contains('scrolled')) {
        window.addEventListener('scroll', function() {
            if (window.scrollY > 50) {
                navbar.classList.add('scrolled');
            } else {
                navbar.classList.remove('scrolled');
            }
        });
    }
    
    // ========================================
    // MOBILE MENU
    // ========================================
    const mobileMenuBtn = document.getElementById('mobileMenuBtn');
    const sidebar = document.getElementById('sidebar') || document.getElementById('adminSidebar');
    const sidebarOverlay = document.getElementById('sidebarOverlay');
    
    if (mobileMenuBtn && sidebar) {
        mobileMenuBtn.addEventListener('click', function() {
            sidebar.classList.toggle('open');
            if (sidebarOverlay) {
                sidebarOverlay.classList.toggle('active');
            }
            document.body.classList.toggle('menu-open');
        });
    }
    
    if (sidebarOverlay) {
        sidebarOverlay.addEventListener('click', function() {
            sidebar.classList.remove('open');
            sidebarOverlay.classList.remove('active');
            document.body.classList.remove('menu-open');
        });
    }
    
    // ========================================
    // SCROLL REVEAL ANIMATION
    // ========================================
    const scrollRevealElements = document.querySelectorAll('.scroll-reveal');
    
    if (scrollRevealElements.length > 0) {
        const observerOptions = {
            threshold: 0.1,
            rootMargin: '0px 0px -50px 0px'
        };
        
        const observer = new IntersectionObserver(function(entries) {
            entries.forEach(function(entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('revealed');
                }
            });
        }, observerOptions);
        
        scrollRevealElements.forEach(function(el) {
            observer.observe(el);
        });
    }
    
    // ========================================
    // WISHLIST TOGGLE
    // ========================================
    const wishlistButtons = document.querySelectorAll('.package-wishlist');
    
    wishlistButtons.forEach(function(btn) {
        btn.addEventListener('click', function(e) {
            e.preventDefault();
            e.stopPropagation();
            
            const icon = this.querySelector('i');
            
            if (icon.classList.contains('far')) {
                icon.classList.remove('far');
                icon.classList.add('fas');
                icon.style.color = '#ef4444';
                showToast('Added to wishlist!', 'success');
            } else {
                icon.classList.remove('fas');
                icon.classList.add('far');
                icon.style.color = '';
                showToast('Removed from wishlist', 'info');
            }
        });
    });
    
    // ========================================
    // TOAST NOTIFICATIONS
    // ========================================
    function showToast(message, type = 'info') {
        // Remove existing toast
        const existingToast = document.querySelector('.toast-notification');
        if (existingToast) {
            existingToast.remove();
        }
        
        // Create toast element
        const toast = document.createElement('div');
        toast.className = 'toast-notification ' + type;
        toast.innerHTML = `
            <i class="fas ${type === 'success' ? 'fa-check-circle' : type === 'error' ? 'fa-exclamation-circle' : 'fa-info-circle'}"></i>
            <span>${message}</span>
        `;
        
        // Add styles
        toast.style.cssText = `
            position: fixed;
            bottom: 24px;
            right: 24px;
            background: var(--color-charcoal);
            border: 1px solid var(--color-slate);
            border-radius: var(--radius-lg);
            padding: 16px 24px;
            display: flex;
            align-items: center;
            gap: 12px;
            color: var(--color-white);
            font-size: 0.9375rem;
            z-index: 10000;
            animation: slideInUp 0.3s ease-out;
            box-shadow: 0 10px 30px rgba(0, 0, 0, 0.3);
        `;
        
        if (type === 'success') {
            toast.querySelector('i').style.color = '#22c55e';
        } else if (type === 'error') {
            toast.querySelector('i').style.color = '#ef4444';
        } else {
            toast.querySelector('i').style.color = 'var(--color-gold)';
        }
        
        document.body.appendChild(toast);
        
        // Auto remove after 3 seconds
        setTimeout(function() {
            toast.style.animation = 'fadeOut 0.3s ease-out forwards';
            setTimeout(function() {
                toast.remove();
            }, 300);
        }, 3000);
    }
    
    // Make showToast globally available
    window.showToast = showToast;
    
    // ========================================
    // FORM VALIDATION
    // ========================================
    const forms = document.querySelectorAll('form[data-validate]');
    
    forms.forEach(function(form) {
        form.addEventListener('submit', function(e) {
            let isValid = true;
            const requiredFields = form.querySelectorAll('[required]');
            
            requiredFields.forEach(function(field) {
                if (!field.value.trim()) {
                    isValid = false;
                    field.classList.add('is-invalid');
                } else {
                    field.classList.remove('is-invalid');
                }
            });
            
            // Email validation
            const emailFields = form.querySelectorAll('[type="email"]');
            emailFields.forEach(function(field) {
                const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
                if (field.value && !emailRegex.test(field.value)) {
                    isValid = false;
                    field.classList.add('is-invalid');
                }
            });
            
            if (!isValid) {
                e.preventDefault();
                showToast('Please fill in all required fields correctly', 'error');
            }
        });
    });
    
    // ========================================
    // DYNAMIC PRICING CALCULATOR
    // ========================================
    const guestsSelect = document.getElementById('guests');
    const bookingSummary = document.querySelector('.booking-summary');
    
    if (guestsSelect && bookingSummary) {
        const basePrice = 1299; // This would come from the page/model
        const serviceFeeRate = 0.05;
        
        guestsSelect.addEventListener('change', function() {
            const guests = parseInt(this.value) || 1;
            const subtotal = basePrice * guests;
            const serviceFee = Math.round(subtotal * serviceFeeRate);
            const total = subtotal + serviceFee;
            
            const rows = bookingSummary.querySelectorAll('.booking-summary-row');
            if (rows.length >= 3) {
                rows[0].innerHTML = `<span>$${basePrice.toLocaleString()} × ${guests} traveler${guests > 1 ? 's' : ''}</span><span>$${subtotal.toLocaleString()}</span>`;
                rows[1].innerHTML = `<span>Service fee</span><span>$${serviceFee.toLocaleString()}</span>`;
                rows[2].innerHTML = `<span>Total</span><span>$${total.toLocaleString()}</span>`;
            }
        });
    }
    
    // ========================================
    // SMOOTH SCROLL
    // ========================================
    const smoothScrollLinks = document.querySelectorAll('a[href^="#"]');
    
    smoothScrollLinks.forEach(function(link) {
        link.addEventListener('click', function(e) {
            const targetId = this.getAttribute('href');
            if (targetId === '#') return;
            
            const targetElement = document.querySelector(targetId);
            if (targetElement) {
                e.preventDefault();
                const navbarHeight = navbar ? navbar.offsetHeight : 0;
                const targetPosition = targetElement.offsetTop - navbarHeight - 20;
                
                window.scrollTo({
                    top: targetPosition,
                    behavior: 'smooth'
                });
            }
        });
    });
    
    // ========================================
    // COUNTDOWN TIMER (for special offers)
    // ========================================
    const countdownElements = document.querySelectorAll('[data-countdown]');
    
    countdownElements.forEach(function(el) {
        const endDate = new Date(el.dataset.countdown).getTime();
        
        const timer = setInterval(function() {
            const now = new Date().getTime();
            const distance = endDate - now;
            
            if (distance < 0) {
                clearInterval(timer);
                el.innerHTML = 'Offer Expired';
                return;
            }
            
            const days = Math.floor(distance / (1000 * 60 * 60 * 24));
            const hours = Math.floor((distance % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
            const minutes = Math.floor((distance % (1000 * 60 * 60)) / (1000 * 60));
            const seconds = Math.floor((distance % (1000 * 60)) / 1000);
            
            el.innerHTML = `${days}d ${hours}h ${minutes}m ${seconds}s`;
        }, 1000);
    });
    
    // ========================================
    // IMAGE LAZY LOADING
    // ========================================
    if ('IntersectionObserver' in window) {
        const lazyImages = document.querySelectorAll('img[data-src]');
        
        const imageObserver = new IntersectionObserver(function(entries) {
            entries.forEach(function(entry) {
                if (entry.isIntersecting) {
                    const img = entry.target;
                    img.src = img.dataset.src;
                    img.removeAttribute('data-src');
                    imageObserver.unobserve(img);
                }
            });
        });
        
        lazyImages.forEach(function(img) {
            imageObserver.observe(img);
        });
    }
    
});

// Add CSS for toast animation
const style = document.createElement('style');
style.textContent = `
    @keyframes slideInUp {
        from {
            opacity: 0;
            transform: translateY(20px);
        }
        to {
            opacity: 1;
            transform: translateY(0);
        }
    }
    
    @keyframes fadeOut {
        from {
            opacity: 1;
            transform: translateY(0);
        }
        to {
            opacity: 0;
            transform: translateY(-10px);
        }
    }
`;
document.head.appendChild(style);
