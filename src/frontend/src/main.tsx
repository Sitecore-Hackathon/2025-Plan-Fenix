import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import Title from './components/Title.tsx';
import RichText from './components/RichText.tsx';
import ImageGrid from './components/ImageGrid.tsx';
import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Title title={'Hello World!'} />
    <ImageGrid images={[]} />
    <RichText text={'lorem ipsum'} />
  </StrictMode>,
);
