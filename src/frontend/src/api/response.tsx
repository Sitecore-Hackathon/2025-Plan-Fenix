import React, { createContext, useContext, useState, useEffect, ReactNode } from 'react';

interface CleanedData {
  imageUrls: string[];
  tags: string;
  title: string;
  content: string;
}

interface CleanedDataContext {
  data: CleanedData | null;
  loading: boolean;
  error: string | null;
}

const CleanedDataContext = createContext<CleanedDataContext>({
  data: null,
  loading: false,
  error: null,
});

export const useCleanedData = () => useContext(CleanedDataContext);

interface CleanedDataProviderProps {
  children: ReactNode;
  apiUrl: string;
}

export const CleanedDataProvider: React.FC<CleanedDataProviderProps> = ({ children, apiUrl }) => {
  const [data, setData] = useState<CleanedData | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  const fetchData = async () => {
    try {
      setLoading(true);
      setError(null);

      const fetchOptions = {
        method: 'GET',
        headers: {
          'Accept': 'application/json',
          'Content-Type': 'application/json',
        },
        mode: 'cors' as RequestMode
      };

      const response = await fetch(apiUrl.toString(), fetchOptions);

      if (response.status === 404) {
        throw new Error(`404 Page Not Found`);
      }

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const jsonData = await response.json();

      if (!jsonData?.sitecore?.route?.fields) {
        throw new Error('Invalid data structure API response');
      }

      const fields = jsonData.sitecore.route.fields;

      if (!fields.Images || !Array.isArray(fields.Images)) {
        throw new Error('404 Page Not Found');
      }

      const cleanedData: CleanedData = {
        imageUrls: fields.Images.map((image: any) => {
          const url = image.url;
          return url || '';
        }),
        tags: fields.Tags?.value.split(',') || '',
        title: fields.Title?.value || '',
        content: fields.Content?.value || ''
      };

      setData(cleanedData);
    } catch (error) {
      console.error('Error fetching data:', error);
      setError(error instanceof Error ? error.message : "Unknown error ocurred");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [apiUrl]);

  const contextValue: CleanedDataContext = {
    data,
    loading,
    error,
  };

  return (
    <CleanedDataContext.Provider value={contextValue}>
      {children}
    </CleanedDataContext.Provider>
  );
};