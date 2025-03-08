import { CleanedDataProvider, useCleanedData } from "./api/response";
import Spinner from "./assets/svgs/Spinner";
import ImageGrid from "./components/ImageGrid/ImageGrid";
import NotFound from "./components/NotFound";
import RichText from "./components/RichText";
import Title from "./components/Title";
import { useState } from "react";

const Content = () => {
  const { error, loading } = useCleanedData();

  if (loading) return <Spinner />;
  if (error === '404 Page Not Found') return <NotFound />;
  if (error) return <div>Error: {error}</div>;

  return (
    <main className='max-w-[1200px] mx-auto px-7 lg:px-0 py-20'>
      <Title />
      <ImageGrid />
      <RichText />
    </main>
  );
};

const App: React.FC = () => {
  const [queryParam] = useState<string>(() => {
    try {
      const urlParams = new URLSearchParams(window.location.search);

      return urlParams.get('page') || 'default-value';
    } catch (error) {
      console.error('Error extracting query parameters:', error);

      return 'default-value';
    }
  });

  const apiUrl = `${import.meta.env.VITE_API_URL}/sitecore/api/layout/render/jss?sc_apikey=${import.meta.env.VITE_SITECORE_API_KEY}&item=/${queryParam}`;

  return (
    <CleanedDataProvider apiUrl={apiUrl}>
      <Content />
    </CleanedDataProvider>
  );
};

export default App;
