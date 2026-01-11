// Home Page JavaScript - TRAVEL

// Package type names and badge classes
const packageTypes = {
    0: { name: 'Family', badge: 'badge-family' },
    1: { name: 'Honeymoon', badge: 'badge-honeymoon' },
    2: { name: 'Adventure', badge: 'badge-adventure' },
    3: { name: 'Cruise', badge: 'badge-cruise' },
    4: { name: 'Luxury', badge: 'badge-luxury' },
    5: { name: 'Budget', badge: 'badge-budget' },
    6: { name: 'Cultural', badge: '' },
    7: { name: 'Beach', badge: '' },
    8: { name: 'Mountain', badge: '' }
};

// Default images for packages without images
const defaultImages = [
    'https://images.unsplash.com/photo-1507525428034-b723cf961d3e?w=800&q=80',
    'https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?w=800&q=80',
    'https://images.unsplash.com/photo-1506929562872-bb421503ef21?w=800&q=80',
    'https://images.unsplash.com/photo-1469474968028-56623f02e42e?w=800&q=80',
    'https://images.unsplash.com/photo-1530789253388-582c481c54b0?w=800&q=80'
];

// Check authentication status
function checkAuth() {
    const token = localStorage.getItem('token');
    const user = localStorage.getItem('user');

    if (token && user) {
        const userData = JSON.parse(user);
        document.getElementById('loggedOutNav').classList.add('d-none');
        document.getElementById('loggedInNav').classList.remove('d-none');
        document.getElementById('userName').textContent = `${userData.firstName} ${userData.lastName}`;
        document.getElementById('userAvatar').textContent = `${userData.firstName.charAt(0)}${userData.lastName.charAt(0)}`;
    } else {
        document.getElementById('loggedOutNav').classList.remove('d-none');
        document.getElementById('loggedInNav').classList.add('d-none');
    }
}

// Logout function
function logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/';
}

// Format date
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}

// Calculate duration
function calculateDuration(startDate, endDate) {
    const start = new Date(startDate);
    const end = new Date(endDate);
    const diffTime = Math.abs(end - start);
    const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
    return diffDays;
}

// Render package card
function renderPackageCard(pkg, index) {
    const typeInfo = packageTypes[pkg.packageType] || { name: 'Travel', badge: '' };
    const duration = calculateDuration(pkg.startDate, pkg.endDate);
    const imageUrl = pkg.imageUrl || defaultImages[index % defaultImages.length];
    const hasDiscount = pkg.discountedPrice && pkg.discountedPrice < pkg.price;

    return `
        <div class="package-card animate-fadeInUp">
            <div class="package-image">
                <img src="${imageUrl}" alt="${pkg.destination}">
                <span class="package-badge ${typeInfo.badge}">${typeInfo.name}</span>
                <button class="package-wishlist" title="Add to wishlist">
                    <i class="far fa-heart"></i>
                </button>
            </div>
            <div class="package-content">
                <div class="package-location">
                    <i class="fas fa-map-marker-alt"></i>
                    ${pkg.destination}, ${pkg.country}
                </div>
                <h3 class="package-title">${pkg.destination} ${typeInfo.name} Package</h3>
                <p class="package-description">${pkg.description || 'Experience the beauty and wonder of ' + pkg.destination + ' with our carefully curated travel package.'}</p>
                <div class="package-meta">
                    <div class="package-meta-item">
                        <i class="fas fa-calendar"></i>
                        ${formatDate(pkg.startDate)} - ${formatDate(pkg.endDate)}
                    </div>
                    <div class="package-meta-item">
                        <i class="fas fa-clock"></i>
                        ${duration} Days
                    </div>
                    <div class="package-meta-item">
                        <i class="fas fa-bed"></i>
                        ${pkg.availableRooms} Rooms
                    </div>
                </div>
                <div class="package-footer">
                    <div class="package-price">
                        <span class="package-price-label">Starting from</span>
                        <span class="package-price-value">
                            $${hasDiscount ? pkg.discountedPrice.toFixed(0) : pkg.price.toFixed(0)}
                            ${hasDiscount ? `<span class="old-price">$${pkg.price.toFixed(0)}</span>` : ''}
                        </span>
                    </div>
                    <a href="/packages/${pkg.packageId}" class="package-btn">View Details</a>
                </div>
            </div>
        </div>
    `;
}

// Load packages from API
async function loadPackages() {
    const grid = document.getElementById('packagesGrid');
    const emptyState = document.getElementById('emptyState');

    try {
        const response = await fetch('/api/travelpackages');

        if (response.ok) {
            const packages = await response.json();

            if (packages && packages.length > 0) {
                grid.innerHTML = packages.slice(0, 6).map((pkg, index) => renderPackageCard(pkg, index)).join('');
                emptyState.classList.add('d-none');
            } else {
                grid.innerHTML = '';
                emptyState.classList.remove('d-none');
            }
        } else {
            showSamplePackages();
        }
    } catch (error) {
        console.log('API not available, showing sample packages');
        showSamplePackages();
    }
}

// Show sample packages when API is not available
function showSamplePackages() {
    const grid = document.getElementById('packagesGrid');
    const samplePackages = [
        {
            packageId: 1,
            destination: 'Maldives',
            country: 'Maldives',
            startDate: '2025-02-15',
            endDate: '2025-02-22',
            price: 2499,
            discountedPrice: 1999,
            availableRooms: 5,
            packageType: 1,
            description: 'Escape to paradise with crystal clear waters and overwater villas.',
            imageUrl: 'https://images.unsplash.com/photo-1514282401047-d79a71a590e8?w=800&q=80'
        },
        {
            packageId: 2,
            destination: 'Swiss Alps',
            country: 'Switzerland',
            startDate: '2025-03-01',
            endDate: '2025-03-08',
            price: 3299,
            availableRooms: 8,
            packageType: 2,
            description: 'Experience breathtaking mountain views and world-class skiing.',
            imageUrl: 'https://images.unsplash.com/photo-1531366936337-7c912a4589a7?w=800&q=80'
        },
        {
            packageId: 3,
            destination: 'Santorini',
            country: 'Greece',
            startDate: '2025-04-10',
            endDate: '2025-04-17',
            price: 1899,
            availableRooms: 12,
            packageType: 1,
            description: 'Romantic sunsets and iconic white-washed architecture await.',
            imageUrl: 'https://images.unsplash.com/photo-1570077188670-e3a8d69ac5ff?w=800&q=80'
        },
        {
            packageId: 4,
            destination: 'Tokyo',
            country: 'Japan',
            startDate: '2025-03-25',
            endDate: '2025-04-02',
            price: 2799,
            discountedPrice: 2399,
            availableRooms: 15,
            packageType: 6,
            description: 'Discover ancient temples and cutting-edge technology.',
            imageUrl: 'https://images.unsplash.com/photo-1540959733332-eab4deabeeaf?w=800&q=80'
        },
        {
            packageId: 5,
            destination: 'Bali',
            country: 'Indonesia',
            startDate: '2025-05-01',
            endDate: '2025-05-10',
            price: 1599,
            availableRooms: 20,
            packageType: 0,
            description: 'Tropical paradise with stunning temples and vibrant culture.',
            imageUrl: 'https://images.unsplash.com/photo-1537996194471-e657df975ab4?w=800&q=80'
        },
        {
            packageId: 6,
            destination: 'Dubai',
            country: 'UAE',
            startDate: '2025-02-20',
            endDate: '2025-02-27',
            price: 3999,
            availableRooms: 6,
            packageType: 4,
            description: 'Luxury shopping, ultramodern architecture, and desert adventures.',
            imageUrl: 'https://images.unsplash.com/photo-1512453979798-5ea266f8880c?w=800&q=80'
        }
    ];

    grid.innerHTML = samplePackages.map((pkg, index) => renderPackageCard(pkg, index)).join('');
}

// Navbar scroll effect
function handleScroll() {
    const navbar = document.getElementById('navbar');
    if (window.scrollY > 50) {
        navbar.classList.add('scrolled');
    } else {
        navbar.classList.remove('scrolled');
    }
}

// Initialize
document.addEventListener('DOMContentLoaded', function () {
    checkAuth();
    loadPackages();
    window.addEventListener('scroll', handleScroll);

    // Logout button
    const logoutBtn = document.getElementById('logoutBtn');
    if (logoutBtn) {
        logoutBtn.addEventListener('click', function (e) {
            e.preventDefault();
            logout();
        });
    }

    // Search form
    const searchForm = document.getElementById('searchForm');
    if (searchForm) {
        searchForm.addEventListener('submit', function (e) {
            e.preventDefault();
            const destination = document.getElementById('searchDestination').value;
            const date = document.getElementById('searchDate').value;
            const type = document.getElementById('searchType').value;

            const params = new URLSearchParams();
            if (destination) params.append('destination', destination);
            if (date) params.append('date', date);
            if (type) params.append('type', type);

            window.location.href = `/packages?${params.toString()}`;
        });
    }

    // Wishlist buttons
    document.addEventListener('click', function (e) {
        if (e.target.closest('.package-wishlist')) {
            const btn = e.target.closest('.package-wishlist');
            btn.classList.toggle('active');
            const icon = btn.querySelector('i');
            icon.classList.toggle('far');
            icon.classList.toggle('fas');
        }
    });
});