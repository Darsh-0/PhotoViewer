import { useEffect, useState } from 'react'
import './App.css'

function App() {
  const [images, setImages] = useState([])
  const [selectedFiles, setSelectedFiles] = useState([])
  const [previews, setPreviews] = useState([])
  const [error, setError] = useState('')
  const [uploading, setUploading] = useState(false)

  async function loadImages() {
    try {
      const response = await fetch('/api/images')
      if (!response.ok) throw new Error('Could not load photos.')
      setImages(await response.json())
      setError('')
    } catch {
      setError('Could not connect to the photo server. Make sure the backend is running.')
    }
  }

  useEffect(() => {
    loadImages()
  }, [])

  useEffect(() => {
    const nextPreviews = selectedFiles.map((file) => ({
      name: file.name,
      url: URL.createObjectURL(file),
    }))
    setPreviews(nextPreviews)
    return () => nextPreviews.forEach((preview) => URL.revokeObjectURL(preview.url))
  }, [selectedFiles])

  async function uploadImages(event) {
    event.preventDefault()
    const form = event.currentTarget
    if (selectedFiles.length === 0) return

    setUploading(true)
    setError('')
    const data = new FormData()
    selectedFiles.forEach((file) => data.append('image', file))
    try {
      const response = await fetch('/api/images', { method: 'POST', body: data })
      const result = await response.json()
      if (!response.ok) throw new Error(result.error || 'Upload failed.')
      form.reset()
      setSelectedFiles([])
      await loadImages()
    } catch (uploadError) {
      setError(uploadError.message)
    } finally {
      setUploading(false)
    }
  }

  return (
    <main className="photos-page">
      <header className="page-header">
        <h1>Photos</h1>
        <form className="upload-form" onSubmit={uploadImages}>
          <label className="upload-button">
            Add photos
            <input
              name="image"
              type="file"
              accept="image/jpeg,image/png,image/gif,image/webp"
              multiple
              onChange={(event) => setSelectedFiles(Array.from(event.target.files || []))}
            />
          </label>
          <button type="submit" disabled={uploading || selectedFiles.length === 0}>
            {uploading ? 'Uploading…' : `Upload ${selectedFiles.length || ''}`.trim()}
          </button>
        </form>
      </header>
      {error && <p className="error-message" role="alert">{error}</p>}
      {selectedFiles.length > 0 && (
        <section className="selection-review" aria-label="Selected photos">
          <div className="selection-heading">
            <div>
              <h2>Review your selection</h2>
              <p>{selectedFiles.length} {selectedFiles.length === 1 ? 'photo' : 'photos'} selected. Confirm by choosing Upload.</p>
            </div>
            <button type="button" className="clear-selection" onClick={() => setSelectedFiles([])} disabled={uploading}>
              Clear selection
            </button>
          </div>
          <div className="preview-grid">
            {previews.map((preview, index) => (
              <figure className="preview-item" key={`${preview.name}-${index}`}>
                <img src={preview.url} alt={`Preview of ${preview.name}`} />
                <figcaption>{preview.name}</figcaption>
              </figure>
            ))}
          </div>
        </section>
      )}
      {images.length > 0 ? (
        <section className="photo-grid" aria-label="Your photos">
          {images.map((image) => <img key={image.id} src={image.url} alt="Uploaded photo" />)}
        </section>
      ) : !error && <p className="empty-message">No photos yet. Add one to get started.</p>}
    </main>
  )
}

export default App
