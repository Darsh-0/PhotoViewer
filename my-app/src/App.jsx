import { useCallback, useEffect, useRef, useState } from 'react'
import './App.css'

function pngFileName(id) {
  return `${id.replace(/\.[^.]+$/, '')}.png`
}

function App() {
  const isAdminPage = window.location.pathname.replace(/\/+$/, '') === '/admin'
  return isAdminPage ? <AdminPage /> : <PhotoGallery />
}

function AdminPage() {
  const [authenticated, setAuthenticated] = useState(false)
  const [checkingSession, setCheckingSession] = useState(true)
  const [password, setPassword] = useState('')
  const [signingIn, setSigningIn] = useState(false)
  const [images, setImages] = useState([])
  const [loadingImages, setLoadingImages] = useState(false)
  const [deletingIds, setDeletingIds] = useState(() => new Set())
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  useEffect(() => {
    let cancelled = false
    fetch('/api/admin/session')
      .then(async (response) => {
        if (!response.ok) throw new Error('Could not check admin sign-in.')
        const session = await response.json()
        if (!cancelled) setAuthenticated(session.authenticated)
      })
      .catch((sessionError) => {
        if (!cancelled) setError(sessionError.message)
      })
      .finally(() => {
        if (!cancelled) setCheckingSession(false)
      })
    return () => { cancelled = true }
  }, [])

  useEffect(() => {
    if (!authenticated) return undefined
    let cancelled = false

    async function loadPhotos() {
      setLoadingImages(true)
      setError('')
      try {
        const allImages = []
        let offset = 0
        let hasMore = true
        while (hasMore) {
          const response = await fetch(`/api/images?offset=${offset}&limit=48`)
          if (response.status === 401) {
            if (!cancelled) setAuthenticated(false)
            return
          }
          if (!response.ok) throw new Error('Could not load photos.')
          const page = await response.json()
          allImages.push(...page.items)
          offset += page.items.length
          hasMore = page.hasMore
        }
        if (!cancelled) setImages(allImages)
      } catch (loadError) {
        if (!cancelled) setError(loadError.message)
      } finally {
        if (!cancelled) setLoadingImages(false)
      }
    }

    loadPhotos()
    return () => { cancelled = true }
  }, [authenticated])

  async function signIn(event) {
    event.preventDefault()
    setSigningIn(true)
    setError('')
    try {
      const response = await fetch('/api/admin/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password }),
      })
      const result = await response.json().catch(() => ({}))
      if (!response.ok) throw new Error(result.error || result.detail || 'Could not sign in.')
      setPassword('')
      setAuthenticated(true)
    } catch (signInError) {
      setError(signInError.message)
    } finally {
      setSigningIn(false)
    }
  }

  async function signOut() {
    setError('')
    try {
      const response = await fetch('/api/admin/logout', { method: 'POST' })
      if (!response.ok) throw new Error('Could not sign out.')
      setAuthenticated(false)
      setImages([])
    } catch (signOutError) {
      setError(signOutError.message)
    }
  }

  async function deletePhoto(image) {
    if (!window.confirm('Delete this photo permanently?')) return
    setDeletingIds((current) => new Set(current).add(image.id))
    setError('')
    setNotice('')
    try {
      const response = await fetch(`/api/images/${encodeURIComponent(image.id)}`, { method: 'DELETE' })
      if (response.status === 401) {
        setAuthenticated(false)
        throw new Error('Your admin session expired. Sign in again to continue.')
      }
      if (!response.ok) throw new Error('Could not delete this photo.')
      setImages((current) => current.filter((photo) => photo.id !== image.id))
      setNotice('Photo deleted.')
    } catch (deleteError) {
      setError(deleteError.message)
    } finally {
      setDeletingIds((current) => {
        const next = new Set(current)
        next.delete(image.id)
        return next
      })
    }
  }

  return (
    <main className="admin-page">
      <header className="admin-header">
        <a className="brand-lockup" href="/">
          <span className="brand-mark" aria-hidden="true">*</span>
          <span>little moments</span>
        </a>
        <a className="admin-back-link" href="/">Back to photos</a>
      </header>

      {checkingSession ? <p className="admin-status">Checking sign-in...</p> : !authenticated ? (
        <section className="admin-login-card">
          <p className="eyebrow">PRIVATE AREA</p>
          <h1>Admin sign in</h1>
          <p>Sign in to manage and delete photos.</p>
          {error && <p className="admin-error" role="alert">{error}</p>}
          <form onSubmit={signIn}>
            <label htmlFor="admin-password">Password</label>
            <input id="admin-password" type="password" autoComplete="current-password" value={password}
              onChange={(event) => setPassword(event.target.value)} required />
            <button className="admin-primary-button" type="submit" disabled={signingIn}>
              {signingIn ? 'Signing in...' : 'Sign in'}
            </button>
          </form>
        </section>
      ) : (
        <section className="admin-content">
          <div className="admin-title-row">
            <div>
              <p className="eyebrow">PHOTO MANAGEMENT</p>
              <h1>Admin panel</h1>
              <p className="admin-subtitle">{images.length} {images.length === 1 ? 'photo' : 'photos'}</p>
            </div>
            <button className="admin-secondary-button" type="button" onClick={signOut}>Sign out</button>
          </div>
          {error && <p className="admin-error" role="alert">{error}</p>}
          {notice && <p className="admin-notice" role="status">{notice}</p>}
          {loadingImages ? <p className="admin-status">Loading photos...</p> : images.length === 0 ? (
            <div className="admin-empty-state">There are no photos to manage.</div>
          ) : (
            <div className="admin-photo-grid">
              {images.map((image, index) => (
                <article className="admin-photo-card" key={image.id}>
                  <img src={image.thumbnailUrl || image.url} alt={`Photo ${index + 1}`} loading="lazy" />
                  <div className="admin-photo-actions">
                    <span title={image.id}>{image.id}</span>
                    <button type="button" className="admin-delete-button" onClick={() => deletePhoto(image)}
                      disabled={deletingIds.has(image.id)}>
                      {deletingIds.has(image.id) ? 'Deleting...' : 'Delete'}
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>
      )}
    </main>
  )
}

function PhotoGallery() {
  const [images, setImages] = useState([])
  const [totalImages, setTotalImages] = useState(0)
  const [hasMoreImages, setHasMoreImages] = useState(false)
  const [loadingMore, setLoadingMore] = useState(false)
  const [pageError, setPageError] = useState('')
  const loadMoreRef = useRef(null)
  const photoGridRef = useRef(null)
  const imagesRef = useRef([])
  const lastLoadedIdRef = useRef(null)
  const [selectedFiles, setSelectedFiles] = useState([])
  const [previews, setPreviews] = useState([])
  const [error, setError] = useState('')
  const [uploading, setUploading] = useState(false)
  const [activeImage, setActiveImage] = useState(null)
  const [uploadDialogOpen, setUploadDialogOpen] = useState(false)
  const [selectedImageIds, setSelectedImageIds] = useState(() => new Set())
  const [downloading, setDownloading] = useState(false)
  const [canShareFiles, setCanShareFiles] = useState(false)
  const [preparedShare, setPreparedShare] = useState(null)

  const addLivePhoto = useCallback((photo) => {
    if (!photo?.id || imagesRef.current.some((image) => image.id === photo.id)) return
    const nextImages = [...imagesRef.current, photo]
    imagesRef.current = nextImages
    setImages(nextImages)
    setTotalImages((count) => Math.max(count + 1, nextImages.length))
  }, [])

  async function loadImages() {
    try {
      const response = await fetch('/api/images?offset=0&limit=24')
      if (!response.ok) throw new Error('Could not load photos.')
      const page = await response.json()
      const pageIds = new Set(page.items.map((image) => image.id))
      const mergedImages = [...page.items, ...imagesRef.current.filter((image) => !pageIds.has(image.id))]
      imagesRef.current = mergedImages
      setImages(mergedImages)
      lastLoadedIdRef.current = page.items.at(-1)?.id || null
      setPreparedShare(null)
      setTotalImages((current) => Math.max(current, page.total, mergedImages.length))
      setHasMoreImages(page.hasMore)
      setPageError('')
      setError('')
    } catch {
      setError('Could not connect to the photo server. Make sure the backend is running.')
    }
  }

  useEffect(() => { loadImages() }, [])

  useEffect(() => {
    let disposed = false
    let reconnectTimer
    let socket

    function connect() {
      if (disposed) return
      const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
      socket = new WebSocket(`${protocol}//${window.location.host}/ws/images`)
      socket.onmessage = (event) => {
        try {
          const message = JSON.parse(event.data)
          if (message.type === 'photo-added') addLivePhoto(message.photo)
        } catch {
          // Ignore malformed or unrelated messages and keep the live connection open.
        }
      }
      socket.onclose = () => {
        if (disposed) return
        reconnectTimer = window.setTimeout(connect, 1500)
      }
      socket.onerror = () => socket.close()
    }

    connect()
    return () => {
      disposed = true
      window.clearTimeout(reconnectTimer)
      socket?.close()
    }
  }, [addLivePhoto])

  useEffect(() => {
    const isMobileDevice = navigator.userAgentData?.mobile ||
      /Android|iPhone|iPad|iPod/i.test(navigator.userAgent) ||
      (/Macintosh/i.test(navigator.userAgent) && navigator.maxTouchPoints > 1)
    if (!isMobileDevice || !navigator.share || !navigator.canShare) return
    try {
      setCanShareFiles(navigator.canShare({ files: [new File([''], 'photo.jpg', { type: 'image/jpeg' })] }))
    } catch {
      setCanShareFiles(false)
    }
  }, [])

  const loadNextPage = useCallback(async () => {
    if (loadingMore || !hasMoreImages) return
    setLoadingMore(true)
    setPageError('')
    try {
      const cursor = lastLoadedIdRef.current
      const query = cursor
        ? `after=${encodeURIComponent(cursor)}&limit=24`
        : `offset=0&limit=24`
      const response = await fetch(`/api/images?${query}`)
      if (!response.ok) throw new Error('Could not load more photos.')
      const page = await response.json()
      if (page.items.length) lastLoadedIdRef.current = page.items.at(-1).id
      const currentIds = new Set(imagesRef.current.map((image) => image.id))
      const newItems = page.items.filter((image) => !currentIds.has(image.id))
      const nextImages = [...imagesRef.current, ...newItems]
      imagesRef.current = nextImages
      setImages(nextImages)
      setTotalImages((current) => Math.max(current, page.total, nextImages.length))
      setHasMoreImages(page.hasMore)
    } catch {
      setPageError('Could not load more photos.')
    } finally {
      setLoadingMore(false)
    }
  }, [hasMoreImages, loadingMore])

  useEffect(() => {
    const target = loadMoreRef.current
    if (!target || !hasMoreImages || pageError || !('IntersectionObserver' in window)) return undefined
    const observer = new IntersectionObserver(([entry]) => {
      if (entry.isIntersecting) loadNextPage()
    }, { rootMargin: '700px 0px' })
    observer.observe(target)
    return () => observer.disconnect()
  }, [hasMoreImages, loadNextPage, pageError])

  useEffect(() => {
    const grid = photoGridRef.current
    if (!grid || !('ResizeObserver' in window)) return undefined

    const updateRowSpan = (image) => {
      if (!image.isConnected) return
      const card = image.closest('.photo-card')
      if (!card) return

      const styles = getComputedStyle(grid)
      const rowHeight = 8
      const rowGap = Number.parseFloat(styles.rowGap)
      const imageHeight = image.getBoundingClientRect().height
      if (imageHeight <= 0) return
      const span = Math.max(1, Math.ceil((imageHeight + rowGap) / (rowHeight + rowGap)))
      card.style.gridRowEnd = `span ${span}`
    }

    const observer = new ResizeObserver((entries) => {
      entries.forEach(({ target }) => updateRowSpan(target))
    })
    const photoImages = grid.querySelectorAll('.photo-card img')
    photoImages.forEach((image) => {
      observer.observe(image)
      updateRowSpan(image)
    })
    grid.classList.add('masonry-ready')

    return () => {
      observer.disconnect()
      grid.classList.remove('masonry-ready')
    }
  }, [images])

  useEffect(() => {
    if (!activeImage && !uploadDialogOpen) return undefined
    function closeOnEscape(event) {
      if (event.key === 'Escape') {
        setActiveImage(null)
        if (!uploading) setUploadDialogOpen(false)
        return
      }
      if (!activeImage || images.length < 2) return
      const currentIndex = images.findIndex((image) => image.id === activeImage.id)
      if (event.key === 'ArrowRight') setActiveImage(images[(currentIndex + 1) % images.length])
      if (event.key === 'ArrowLeft') setActiveImage(images[(currentIndex - 1 + images.length) % images.length])
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [activeImage, images, uploadDialogOpen, uploading])

  useEffect(() => {
    const nextPreviews = selectedFiles.map((file) => ({ name: file.name, url: URL.createObjectURL(file) }))
    setPreviews(nextPreviews)
    return () => nextPreviews.forEach((preview) => URL.revokeObjectURL(preview.url))
  }, [selectedFiles])

  async function uploadImages(event) {
    event.preventDefault()
    if (selectedFiles.length === 0) return
    if (selectedFiles.some((file) => file.size > 100 * 1024 * 1024)) {
      setError('Each photo must be 100 MB or smaller.')
      return
    }
    setUploading(true)
    setError('')
    const data = new FormData()
    selectedFiles.forEach((file) => data.append('image', file))
    try {
      const response = await fetch('/api/images', { method: 'POST', body: data })
      const responseText = await response.text()
      let result = {}
      try {
        result = responseText ? JSON.parse(responseText) : {}
      } catch {
        // ASP.NET can return plain text for request-size errors before the endpoint runs.
      }
      if (!response.ok) {
        const message = result.error || (response.status === 413
          ? 'The upload is too large. Each photo must be 100 MB or smaller.'
          : `Upload failed (${response.status}). Check that the photo is JPG, PNG, GIF, or WebP and try again.`)
        throw new Error(message)
      }
      setSelectedFiles([])
      setUploadDialogOpen(false)
      if (Array.isArray(result)) result.forEach(addLivePhoto)
    } catch (uploadError) {
      setError(uploadError.message)
    } finally {
      setUploading(false)
    }
  }

  function toggleImageSelection(id) {
    setPreparedShare(null)
    setSelectedImageIds((current) => {
      const next = new Set(current)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })
  }

  async function getPhotosForDownload(scope) {
    if (scope === 'selected') return images.filter((image) => selectedImageIds.has(image.id))

    const photos = []
    let offset = 0
    let hasMore = true
    while (hasMore) {
      const response = await fetch(`/api/images?offset=${offset}&limit=48`)
      if (!response.ok) throw new Error('Could not load photos for download.')
      const page = await response.json()
      photos.push(...page.items)
      offset += page.items.length
      hasMore = page.hasMore
    }
    return photos
  }

  function startImageDownloads(photos) {
    photos.forEach((photo, index) => {
      window.setTimeout(() => {
        const link = document.createElement('a')
        const downloadUrl = photo.downloadUrl || photo.url
        link.href = downloadUrl || URL.createObjectURL(photo)
        link.download = photo.downloadUrl ? pngFileName(photo.id) : photo.name || photo.id || 'photo.png'
        document.body.appendChild(link)
        link.click()
        link.remove()
        if (!downloadUrl) window.setTimeout(() => URL.revokeObjectURL(link.href), 1000)
      }, index * 250)
    })
  }

  async function preparePhotosForSharing(scope) {
    setDownloading(true)
    setError('')
    try {
      const photos = await getPhotosForDownload(scope)
      const files = []
      for (let start = 0; start < photos.length; start += 4) {
        const batch = photos.slice(start, start + 4)
        files.push(...await Promise.all(batch.map(async (photo) => {
          const response = await fetch(photo.downloadUrl || photo.url)
          if (!response.ok) throw new Error('Could not prepare photos for sharing.')
          const blob = await response.blob()
          return new File([blob], pngFileName(photo.id), { type: 'image/png' })
        })))
      }
      if (!files.length || !navigator.canShare({ files })) throw new Error('This browser cannot share these photos.')
      setPreparedShare({ scope, files })
      return files
    } catch (shareError) {
      setError(shareError.message || 'Could not prepare photos for sharing.')
      return null
    } finally {
      setDownloading(false)
    }
  }

  async function downloadPhotos(scope) {
    if (canShareFiles) {
      if (preparedShare?.scope !== scope) {
        const files = await preparePhotosForSharing(scope)
        if (!files) await downloadIndividualPhotos(scope)
        return
      }
      try {
        await navigator.share({ files: preparedShare.files, title: 'Photos' })
        setPreparedShare(null)
      } catch (shareError) {
        if (shareError.name !== 'AbortError') {
          setError('Could not open the share sheet. Downloading the image files instead.')
          startImageDownloads(preparedShare.files)
          setPreparedShare(null)
        }
      }
      return
    }

    await downloadIndividualPhotos(scope)
  }

  async function downloadIndividualPhotos(scope) {
    setDownloading(true)
    setError('')
    try {
      const photos = await getPhotosForDownload(scope)
      if (photos.length === 0) throw new Error('Select at least one photo to download.')
      startImageDownloads(photos)
    } catch (downloadError) {
      setError(downloadError.message || 'Could not download photos.')
    } finally {
      setDownloading(false)
    }
  }

  return (
    <main className="photos-page">
      <header className="page-header">
        <div className="brand-lockup"><div className="brand-mark" aria-hidden="true">*</div><span>little moments</span></div>
        <div className="header-actions">
          <button type="button" className="upload-button" onClick={() => setUploadDialogOpen(true)}>+ Add photos</button>
        </div>
      </header>
      <section className="intro-block">
        <div>
          <p className="eyebrow">YOUR PERSONAL PHOTO WALL</p>
          <h1>A place for the<br /><span>good stuff.</span></h1>
          <p className="intro-copy">Keep your favourite snapshots close. Add a few photos and make this space yours.</p>
        </div>
        <div className="photo-count" aria-label={`${totalImages} photos in your collection`}>
          <strong>{String(totalImages).padStart(2, '0')}</strong>
          <span>{totalImages === 1 ? 'photo' : 'photos'}<br />collected</span>
        </div>
      </section>
      {error && <p className="error-message" role="alert">{error}</p>}
      {images.length > 0 ? (
        <section className="gallery-section" aria-label="Your photos">
          <div className="gallery-heading">
            <div><p className="eyebrow">THE COLLECTION</p><h2>Your scrapbook</h2></div>
            <div className="gallery-actions">
              <button type="button" className="download-button" onClick={() => downloadPhotos('all')} disabled={downloading}>
                {downloading ? 'Preparing photos...' : preparedShare?.scope === 'all' ? 'Save all to Photos' : 'Download all'}
              </button>
              <button type="button" className="download-button selected-download" onClick={() => downloadPhotos('selected')} disabled={downloading || selectedImageIds.size === 0}>
                {downloading ? 'Preparing photos...' : preparedShare?.scope === 'selected' ? 'Save selected to Photos' : `Download selected (${selectedImageIds.size})`}
              </button>
            </div>
          </div>
          {canShareFiles && <p className="share-hint">Tap a download button, then tap Save to Photos when it is ready.</p>}
          <div className="photo-grid" ref={photoGridRef}>
            {images.map((image, index) => (
              <div className={`photo-card${selectedImageIds.has(image.id) ? ' is-selected' : ''}`} key={image.id}>
                <button className="photo-view-button" type="button" onClick={() => setActiveImage(image)} aria-label={`Open photo ${index + 1}`}>
                  <img src={image.thumbnailUrl || image.url} alt={`Photo ${index + 1}`} loading="lazy" />
                  <span className="photo-open-hint">View photo &#8599;</span>
                </button>
                <button type="button" className="photo-select-button" onClick={() => toggleImageSelection(image.id)} disabled={downloading} aria-label={`${selectedImageIds.has(image.id) ? 'Deselect' : 'Select'} photo ${index + 1}`} aria-pressed={selectedImageIds.has(image.id)}>
                  {selectedImageIds.has(image.id) ? '✓' : ''}
                </button>
              </div>
            ))}
          </div>
          {hasMoreImages && 'IntersectionObserver' in window && <div className="load-more-sentinel" ref={loadMoreRef} aria-hidden="true" />}
          {hasMoreImages && !('IntersectionObserver' in window) && <button type="button" className="load-more-button" onClick={loadNextPage} disabled={loadingMore}>Load more photos</button>}
          {loadingMore && <p className="loading-more" role="status">Loading more photos...</p>}
          {pageError && <div className="load-more-error"><p>{pageError}</p><button type="button" onClick={loadNextPage}>Try again</button></div>}
        </section>
      ) : !error && (
        <section className="empty-state">
          <div className="empty-illustration" aria-hidden="true">*</div>
          <h2>Your scrapbook starts here</h2>
          <p>Choose a few photos above and they will find a home on your wall.</p>
        </section>
      )}
      {uploadDialogOpen && (
        <div className="lightbox upload-backdrop" role="presentation" onClick={() => !uploading && setUploadDialogOpen(false)}>
          <form className="upload-dialog" role="dialog" aria-modal="true" aria-labelledby="upload-title" onSubmit={uploadImages} onClick={(event) => event.stopPropagation()}>
            <button type="button" className="lightbox-close" onClick={() => setUploadDialogOpen(false)} aria-label="Close upload" disabled={uploading}>&times;</button>
            <p className="eyebrow">ADD TO YOUR PHOTO WALL</p>
            <h2 id="upload-title">Choose your photos</h2>
            <p className="upload-dialog-copy">Pick one or more images, review your selection, then upload them together.</p>
            <label className="file-picker">
              <span>+ Choose photos</span>
              <input name="image" type="file" accept="image/jpeg,image/png,image/gif,image/webp" multiple
                onChange={(event) => {
                  const newlySelectedFiles = Array.from(event.target.files || [])
                  setSelectedFiles((files) => [...files, ...newlySelectedFiles])
                  event.target.value = ''
                }} />
            </label>
            {selectedFiles.length > 0 && (
              <>
                <div className="selection-heading">
                  <p>{selectedFiles.length} {selectedFiles.length === 1 ? 'photo' : 'photos'} selected</p>
                  <button type="button" className="clear-selection" onClick={() => setSelectedFiles([])} disabled={uploading}>Clear all</button>
                </div>
                <div className="preview-grid">
                  {previews.map((preview, index) => (
                    <figure className="preview-item" key={`${preview.name}-${index}`}>
                      <button type="button" className="remove-preview" aria-label={`Remove ${preview.name}`} onClick={() => setSelectedFiles((files) => files.filter((_, fileIndex) => fileIndex !== index))} disabled={uploading}>&times;</button>
                      <img src={preview.url} alt={`Preview of ${preview.name}`} />
                      <figcaption>{preview.name}</figcaption>
                    </figure>
                  ))}
                </div>
              </>
            )}
            <div className="upload-dialog-actions">
              <button type="button" className="cancel-upload" onClick={() => setUploadDialogOpen(false)} disabled={uploading}>Cancel</button>
              <button type="submit" className="confirm-upload" disabled={uploading || selectedFiles.length === 0}>
                {uploading ? 'Uploading...' : `Upload ${selectedFiles.length || ''}`.trim()}
              </button>
            </div>
          </form>
        </div>
      )}
      {activeImage && (
        <div className="lightbox" role="presentation" onClick={() => setActiveImage(null)}>
          <div className="lightbox-panel" role="dialog" aria-modal="true" aria-label="Photo preview" onClick={(event) => event.stopPropagation()}>
            <button type="button" className="lightbox-close" onClick={() => setActiveImage(null)} aria-label="Close photo">&times;</button>
            {images.length > 1 && (
              <>
                <button type="button" className="lightbox-nav lightbox-previous" onClick={() => {
                  const index = images.findIndex((image) => image.id === activeImage.id)
                  setActiveImage(images[(index - 1 + images.length) % images.length])
                }} aria-label="Previous photo">&#8592;</button>
                <button type="button" className="lightbox-nav lightbox-next" onClick={() => {
                  const index = images.findIndex((image) => image.id === activeImage.id)
                  setActiveImage(images[(index + 1) % images.length])
                }} aria-label="Next photo">&#8594;</button>
              </>
            )}
            <img src={activeImage.url} alt="Enlarged photo" />
            <span className="lightbox-count">{images.findIndex((image) => image.id === activeImage.id) + 1} of {images.length}</span>
            <a href={activeImage.downloadUrl || activeImage.url} download={pngFileName(activeImage.id)} className="download-link">Download photo &#8595;</a>
          </div>
        </div>
      )}
    </main>
  )
}

export default App
