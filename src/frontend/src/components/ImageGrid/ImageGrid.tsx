import { useCleanedData } from '../../api/response';
import './ImageGrid.css';

const ImageGrid = () => {
  const { data } = useCleanedData();

  return (
    <section className='mb-20'>
      <p className="text-lg mb-7 font-bold">
        Suggested images for this content:
      </p>
      <div className="image-grid">
        {data?.imageUrls?.map((imageUrl, index) => (
          <img
            key={`image-${index}`}
            src={imageUrl}
            className='rounded-3xl max-h-[255px] object-cover object-center self-center'
          />
        ))}
      </div>
    </section>
  );
};

export default ImageGrid;