// Alpine.js CMS Editor
function cmsEditor() {
    return {
        // State
        pageTitle: 'Homepage',
        lastSaved: 'Never',
        author: 'Admin',
        blocks: [],
        selectedBlock: null,
        draggedBlock: null,
        draggedType: null,
        draggedIndex: null,
        
        // UI State
        sidebarCollapsed: false,
        previewMode: false,
        previewDevice: 'desktop',
        showBlockMenu: false,
        
        // Categories
        categories: {
            content: true,
            media: true,
            references: true
        },
        
        // Media Modal
        mediaModalOpen: false,
        mediaTab: 'gallery',
        selectedMedia: [],
        currentBlock: null,
        isGallery: false,
        mediaContext: null, // 'background', 'nested', etc.
        nestedContext: null, // { block, colIndex, nestedBlock }
        
        // Media Library
        mediaLibrary: [
            { id: 1, src: 'https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=400', alt: 'Mountain landscape' },
            { id: 2, src: 'https://images.unsplash.com/photo-1469474968028-56623f02e42e?w=400', alt: 'Nature scene' },
            { id: 3, src: 'https://images.unsplash.com/photo-1447752875215-b2761acb3c5d?w=400', alt: 'Forest path' },
            { id: 4, src: 'https://images.unsplash.com/photo-1433086966358-54859d0ed716?w=400', alt: 'Waterfall' },
            { id: 5, src: 'https://images.unsplash.com/photo-1501785888041-af3ef285b470?w=400', alt: 'Lake view' },
            { id: 6, src: 'https://images.unsplash.com/photo-1470071459604-3b5ec3a7fe05?w=400', alt: 'Foggy mountains' }
        ],
        
        // Reference Data
        referenceData: {
            pages: [
                { id: 1, title: 'About Us' },
                { id: 2, title: 'Contact' },
                { id: 3, title: 'Services' },
                { id: 4, title: 'Portfolio' }
            ],
            posts: [
                { id: 1, title: 'Getting Started with Aero CMS' },
                { id: 2, title: 'Best Practices for Content Management' },
                { id: 3, title: 'SEO Tips for Your Website' }
            ],
            categories: [
                { id: 1, name: 'Technology' },
                { id: 2, name: 'Design' },
                { id: 3, name: 'Business' },
                { id: 4, name: 'Lifestyle' }
            ],
            tags: [
                { id: 1, name: 'cms' },
                { id: 2, name: 'webdev' },
                { id: 3, name: 'design' },
                { id: 4, name: 'tutorial' }
            ],
            authors: [
                { id: 1, name: 'John Doe' },
                { id: 2, name: 'Jane Smith' },
                { id: 3, name: 'Mike Johnson' }
            ]
        },
        
        // Toasts
        toasts: [],
        
        // TinyMCE Editors
        editors: {},
        
        init() {
            this.updateLastSaved();
            
            // Auto-save every 30 seconds
            setInterval(() => {
                this.autoSave();
            }, 30000);
            
            // Keyboard shortcuts
            document.addEventListener('keydown', (e) => {
                if ((e.ctrlKey || e.metaKey) && e.key === 's') {
                    e.preventDefault();
                    this.savePage();
                }
            });
        },
        
        // Category Toggle
        toggleCategory(category) {
            this.categories[category] = !this.categories[category];
        },
        
        // Block Management
        addBlock(type) {
            const id = Date.now().toString();
            let block = { id, type };
            
            switch (type) {
                case 'hero':
                    block.mainText = '';
                    block.subText = '';
                    block.ctaText = '';
                    block.ctaUrl = '';
                    block.backgroundImage = '';
                    break;
                case 'text':
                    block.content = '';
                    break;
                case 'content':
                    block.content = '<p>Start typing your content here...</p>';
                    break;
                case 'markdown':
                    block.content = '# Heading\n\nYour markdown content here...';
                    block.view = 'edit';
                    break;
                case 'quote':
                    block.content = '';
                    block.author = '';
                    break;
                case 'separator':
                    break;
                case 'columns':
                    block.columnCount = 2;
                    block.gap = 16;
                    block.columns = [
                        { id: Date.now() + '1', blocks: [] },
                        { id: Date.now() + '2', blocks: [] }
                    ];
                    break;
                case 'image':
                    block.src = '';
                    block.alt = '';
                    block.caption = '';
                    break;
                case 'video':
                    block.url = '';
                    block.src = '';
                    break;
                case 'gallery':
                    block.images = [];
                    break;
                case 'audio':
                    block.src = '';
                    break;
                case 'pages':
                case 'posts':
                case 'categories':
                case 'tags':
                case 'authors':
                    block.selected = '';
                    break;
            }
            
            this.blocks.push(block);
            this.selectBlock(id);
            
            // Initialize editor for content blocks
            if (type === 'content') {
                this.$nextTick(() => {
                    this.initEditor(block);
                });
            }
            
            this.showToast('Block added', 'success');
        },
        
        selectBlock(id) {
            this.selectedBlock = id;
        },
        
        deleteBlock(index) {
            const block = this.blocks[index];
            if (block.type === 'content' && this.editors[block.id]) {
                tinymce.remove('#editor-' + block.id);
                delete this.editors[block.id];
            }
            this.blocks.splice(index, 1);
            this.selectedBlock = null;
            this.showToast('Block deleted', 'info');
        },
        
        duplicateBlock(index) {
            const block = JSON.parse(JSON.stringify(this.blocks[index]));
            block.id = Date.now().toString();
            
            // Regenerate column IDs for columns block
            if (block.type === 'columns') {
                block.columns = block.columns.map((col, i) => ({
                    ...col,
                    id: Date.now() + i.toString()
                }));
            }
            
            this.blocks.splice(index + 1, 0, block);
            
            if (block.type === 'content') {
                this.$nextTick(() => {
                    this.initEditor(block);
                });
            }
            
            this.showToast('Block duplicated', 'success');
        },
        
        moveBlockUp(index) {
            if (index > 0) {
                const temp = this.blocks[index];
                this.blocks[index] = this.blocks[index - 1];
                this.blocks[index - 1] = temp;
            }
        },
        
        moveBlockDown(index) {
            if (index < this.blocks.length - 1) {
                const temp = this.blocks[index];
                this.blocks[index] = this.blocks[index + 1];
                this.blocks[index + 1] = temp;
            }
        },
        
        // Drag & Drop
        dragStart(event, type) {
            this.draggedType = type;
            event.dataTransfer.effectAllowed = 'copy';
            event.dataTransfer.setData('block-type', type);
        },
        
        dragStartBlock(event, id, index) {
            this.draggedBlock = id;
            this.draggedIndex = index;
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('block-id', id);
        },
        
        dragOver(event) {
            event.preventDefault();
        },
        
        dragOverBlock(event, index) {
            event.preventDefault();
            if (this.draggedIndex !== null && this.draggedIndex !== index) {
                const block = this.blocks[this.draggedIndex];
                this.blocks.splice(this.draggedIndex, 1);
                this.blocks.splice(index, 0, block);
                this.draggedIndex = index;
            }
        },
        
        drop(event) {
            event.preventDefault();
            const type = event.dataTransfer.getData('block-type');
            if (type && this.draggedType) {
                this.addBlock(this.draggedType);
                this.draggedType = null;
            }
        },
        
        dropBlock(event, index) {
            event.preventDefault();
            this.draggedBlock = null;
            this.draggedIndex = null;
        },
        
        // Column Management
        updateColumnCount(block) {
            const currentCount = block.columns.length;
            const newCount = parseInt(block.columnCount);
            
            if (newCount > currentCount) {
                // Add columns
                for (let i = currentCount; i < newCount; i++) {
                    block.columns.push({
                        id: Date.now() + i.toString(),
                        blocks: []
                    });
                }
            } else if (newCount < currentCount) {
                // Remove columns (with warning if they have content)
                const columnsToRemove = block.columns.slice(newCount);
                const hasContent = columnsToRemove.some(col => col.blocks && col.blocks.length > 0);
                
                if (hasContent) {
                    if (!confirm('Some columns have content. Removing them will delete that content. Continue?')) {
                        block.columnCount = currentCount;
                        return;
                    }
                }
                
                block.columns = block.columns.slice(0, newCount);
            }
        },
        
        addBlockToColumn(block, colIndex, type) {
            const nestedBlock = this.createNestedBlock(type);
            block.columns[colIndex].blocks.push(nestedBlock);
        },
        
        createNestedBlock(type) {
            const id = Date.now().toString();
            let block = { id, type };
            
            switch (type) {
                case 'text':
                    block.content = '';
                    break;
                case 'image':
                    block.src = '';
                    block.alt = '';
                    break;
                case 'video':
                    block.url = '';
                    block.src = '';
                    break;
                case 'button':
                    block.text = 'Click Me';
                    block.url = '#';
                    block.style = 'primary';
                    break;
            }
            
            return block;
        },
        
        removeNestedBlock(block, colIndex, nestedIndex) {
            block.columns[colIndex].blocks.splice(nestedIndex, 1);
        },
        
        dragOverColumn(event, block, colIndex) {
            event.preventDefault();
            event.stopPropagation();
            event.dataTransfer.dropEffect = 'copy';
        },
        
        dropOnColumn(event, block, colIndex) {
            event.preventDefault();
            event.stopPropagation();
            
            const type = event.dataTransfer.getData('block-type');
            if (type) {
                // Map sidebar types to nested types
                const nestedTypeMap = {
                    'text': 'text',
                    'image': 'image',
                    'video': 'video'
                };
                
                const nestedType = nestedTypeMap[type];
                if (nestedType) {
                    const nestedBlock = this.createNestedBlock(nestedType);
                    block.columns[colIndex].blocks.push(nestedBlock);
                    this.showToast(`${type} added to column`, 'success');
                }
            }
        },
        
        // Rich Text Editor
        initEditor(block) {
            const editorId = 'editor-' + block.id;
            
            if (this.editors[block.id]) {
                return;
            }
            
            tinymce.init({
                selector: '#' + editorId,
                height: 300,
                menubar: false,
                plugins: [
                    'advlist', 'autolink', 'lists', 'link', 'image', 'charmap', 'preview',
                    'anchor', 'searchreplace', 'visualblocks', 'code', 'fullscreen',
                    'insertdatetime', 'media', 'table', 'help', 'wordcount'
                ],
                toolbar: 'undo redo | blocks | ' +
                    'bold italic forecolor | alignleft aligncenter ' +
                    'alignright alignjustify | bullist numlist outdent indent | ' +
                    'removeformat | help',
                content_style: `
                    body { 
                        font-family: Inter, sans-serif; 
                        font-size: 15px; 
                        color: #f8fafc;
                        background: #0f172a;
                        padding: 16px;
                    }
                    p { margin: 0 0 12px; line-height: 1.6; }
                    h1, h2, h3, h4 { margin: 0 0 16px; color: #f8fafc; }
                    a { color: #818cf8; }
                `,
                setup: (editor) => {
                    editor.on('change', () => {
                        block.content = editor.getContent();
                    });
                }
            }).then((editors) => {
                if (editors.length > 0) {
                    this.editors[block.id] = editors[0];
                    editors[0].setContent(block.content);
                }
            });
        },
        
        // Markdown
        renderMarkdown(content) {
            if (!content) return '';
            
            // Simple markdown parser
            let html = content
                .replace(/^# (.*$)/gim, '<h1>$1</h1>')
                .replace(/^## (.*$)/gim, '<h2>$1</h2>')
                .replace(/^### (.*$)/gim, '<h3>$1</h3>')
                .replace(/^#### (.*$)/gim, '<h4>$1</h4>')
                .replace(/\*\*(.*)\*\*/gim, '<strong>$1</strong>')
                .replace(/\*(.*)\*/gim, '<em>$1</em>')
                .replace(/`(.*?)`/gim, '<code>$1</code>')
                .replace(/```([\s\S]*?)```/gim, '<pre><code>$1</code></pre>')
                .replace(/\[([^\]]+)\]\(([^)]+)\)/gim, '<a href="$2">$1</a>')
                .replace(/^- (.*$)/gim, '<li>$1</li>')
                .replace(/(<li>.*<\/li>)/s, '<ul>$1</ul>');
            
            // Wrap paragraphs
            const lines = html.split('\n');
            html = lines.map(line => {
                if (line.trim() && !line.startsWith('<')) {
                    return '<p>' + line + '</p>';
                }
                return line;
            }).join('');
            
            return html;
        },
        
        // Media Selector
        openMediaSelector(block, isGallery = false, context = null) {
            this.currentBlock = block;
            this.isGallery = isGallery;
            this.mediaContext = context;
            this.nestedContext = null;
            this.selectedMedia = [];
            this.mediaModalOpen = true;
            this.mediaTab = 'gallery';
        },
        
        openMediaSelectorForNested(block, colIndex, nestedBlock) {
            this.currentBlock = block;
            this.isGallery = false;
            this.mediaContext = 'nested';
            this.nestedContext = { block, colIndex, nestedBlock };
            this.selectedMedia = [];
            this.mediaModalOpen = true;
            this.mediaTab = 'gallery';
        },
        
        openAudioSelector(block) {
            // Simulate audio selection
            block.src = 'https://www.soundhelix.com/examples/mp3/SoundHelix-Song-1.mp3';
            this.showToast('Audio added', 'success');
        },
        
        toggleMediaSelection(img) {
            if (this.isGallery) {
                const index = this.selectedMedia.indexOf(img.id);
                if (index > -1) {
                    this.selectedMedia.splice(index, 1);
                } else {
                    this.selectedMedia.push(img.id);
                }
            } else {
                this.selectedMedia = [img.id];
            }
        },
        
        confirmMediaSelection() {
            const selectedImages = this.mediaLibrary.filter(img => 
                this.selectedMedia.includes(img.id)
            );
            
            if (this.mediaContext === 'background') {
                // Hero background image
                if (selectedImages.length > 0) {
                    this.currentBlock.backgroundImage = selectedImages[0].src;
                }
            } else if (this.mediaContext === 'nested' && this.nestedContext) {
                // Nested image in column
                if (selectedImages.length > 0) {
                    this.nestedContext.nestedBlock.src = selectedImages[0].src;
                    this.nestedContext.nestedBlock.alt = selectedImages[0].alt;
                }
            } else if (this.isGallery) {
                this.currentBlock.images.push(...selectedImages.map(img => ({
                    src: img.src,
                    alt: img.alt
                })));
            } else {
                if (selectedImages.length > 0) {
                    this.currentBlock.src = selectedImages[0].src;
                    this.currentBlock.alt = selectedImages[0].alt;
                }
            }
            
            this.mediaModalOpen = false;
            this.showToast('Media added', 'success');
        },
        
        removeImage(block) {
            block.src = '';
            block.alt = '';
            block.caption = '';
        },
        
        removeGalleryImage(block, index) {
            block.images.splice(index, 1);
        },
        
        // File Upload
        handleFileDrop(event) {
            event.preventDefault();
            const files = event.dataTransfer.files;
            this.processFiles(files);
        },
        
        handleFileSelect(event) {
            const files = event.target.files;
            this.processFiles(files);
        },
        
        processFiles(files) {
            Array.from(files).forEach(file => {
                if (file.type.startsWith('image/')) {
                    const reader = new FileReader();
                    reader.onload = (e) => {
                        const newImage = {
                            id: Date.now() + Math.random(),
                            src: e.target.result,
                            alt: file.name
                        };
                        this.mediaLibrary.unshift(newImage);
                    };
                    reader.readAsDataURL(file);
                }
            });
            this.showToast('Files uploaded', 'success');
        },
        
        // Video
        loadVideo(block) {
            const url = block.url;
            let embedUrl = '';
            
            // YouTube
            const youtubeMatch = url.match(/(?:youtube\.com\/watch\?v=|youtu\.be\/)([^&\s]+)/);
            if (youtubeMatch) {
                embedUrl = `https://www.youtube.com/embed/${youtubeMatch[1]}`;
            }
            
            // Vimeo
            const vimeoMatch = url.match(/vimeo\.com\/(\d+)/);
            if (vimeoMatch) {
                embedUrl = `https://player.vimeo.com/video/${vimeoMatch[1]}`;
            }
            
            // Direct video
            if (url.match(/\.(mp4|webm|ogg)$/i)) {
                embedUrl = url;
            }
            
            if (embedUrl) {
                block.src = embedUrl;
                this.showToast('Video added', 'success');
            } else {
                this.showToast('Invalid video URL', 'error');
            }
        },
        
        loadNestedVideo(block, colIndex, nestedBlock) {
            const url = nestedBlock.url;
            let embedUrl = '';
            
            const youtubeMatch = url.match(/(?:youtube\.com\/watch\?v=|youtu\.be\/)([^&\s]+)/);
            if (youtubeMatch) {
                embedUrl = `https://www.youtube.com/embed/${youtubeMatch[1]}`;
            }
            
            const vimeoMatch = url.match(/vimeo\.com\/(\d+)/);
            if (vimeoMatch) {
                embedUrl = `https://player.vimeo.com/video/${vimeoMatch[1]}`;
            }
            
            if (url.match(/\.(mp4|webm|ogg)$/i)) {
                embedUrl = url;
            }
            
            if (embedUrl) {
                nestedBlock.src = embedUrl;
            }
        },
        
        removeVideo(block) {
            block.src = '';
            block.url = '';
        },
        
        // Audio
        removeAudio(block) {
            block.src = '';
        },
        
        // References
        getReferenceItems(type) {
            return this.referenceData[type] || [];
        },
        
        updateReference(block) {
            // Reference updated
        },
        
        renderReferencePreview(block) {
            const items = this.getReferenceItems(block.type);
            const item = items.find(i => i.id == block.selected);
            
            if (!item) return '';
            
            if (block.type === 'pages' || block.type === 'posts') {
                return `<div class="reference-card">
                    <h4>${item.title}</h4>
                    <span class="reference-type">${block.type.slice(0, -1)}</span>
                </div>`;
            } else if (block.type === 'authors') {
                return `<div class="reference-card author">
                    <div class="author-avatar">${item.name.charAt(0)}</div>
                    <span class="author-name">${item.name}</span>
                </div>`;
            } else {
                return `<div class="reference-card tag">
                    <span class="tag-name">${item.name}</span>
                </div>`;
            }
        },
        
        // Preview
        togglePreview() {
            this.previewMode = !this.previewMode;
            if (this.previewMode) {
                this.selectedBlock = null;
            }
        },
        
        // Save & Publish
        savePage() {
            const data = {
                title: this.pageTitle,
                blocks: this.blocks,
                savedAt: new Date().toISOString()
            };
            
            localStorage.setItem('aero-cms-page', JSON.stringify(data));
            this.updateLastSaved();
            this.showToast('Page saved successfully', 'success');
        },
        
        autoSave() {
            if (this.blocks.length > 0) {
                this.savePage();
            }
        },
        
        publishPage() {
            this.savePage();
            this.showToast('Page published!', 'success');
        },
        
        updateLastSaved() {
            const now = new Date();
            this.lastSaved = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        },
        
        // Toast Notifications
        showToast(message, type = 'info') {
            const id = Date.now();
            this.toasts.push({ id, message, type });
            
            setTimeout(() => {
                this.removeToast(id);
            }, 4000);
        },
        
        removeToast(id) {
            const index = this.toasts.findIndex(t => t.id === id);
            if (index > -1) {
                this.toasts.splice(index, 1);
            }
        }
    };
}

// Initialize Alpine.js
document.addEventListener('alpine:init', () => {
    Alpine.data('cmsEditor', cmsEditor);
});
