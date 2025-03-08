interface ImageGridProps {
  images: string[];
}

const ImageGrid: React.FC<ImageGridProps> = ({ images }) => {
  if (!images || images.length === 0) {
    return <p className="text-gray-500">No images to display</p>
  }

  return (
    <section className="grid">
      {images.map((imageUrl, index) => (
        <img
          key={`image-${index}`}
          src={imageUrl}
        />
      ))}
    </section>
  );
};

export default ImageGrid;