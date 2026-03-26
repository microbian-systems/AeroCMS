declare global {
    interface Array<T> {
        /**
         * Determines whether all elements of a sequence satisfy a condition.
         * @param predicate - A function to test each element for a condition.
         * @returns boolean - true if every element of the source sequence passes the test in the specified predicate, or if the sequence is empty; otherwise, false.
         */
        all(predicate: (item: T, index: number) => boolean): boolean;
        /**
         * Determines whether a sequence contains any elements.
         * @param predicate - A function to test each element for a condition.
         * @returns boolean - true if every element of the source sequence passes the test in the specified predicate, or if the sequence is empty; otherwise, false.
         */
        any(predicate?: (item: T, index: number) => boolean): boolean;
        /**
         * Computes the sum of a sequence of nullable number values.
         * @param selector - A transform function to apply to each element.
         * @returns number - the sum of the values in the sequence.
         */
        average(selector?: ((item: T) => number | null | undefined) | null): number;
        /**
         * Filters a sequence of values based on a predicate.
         * @param predicate - A function to test each element for a condition.
         * @returns Array<T> - An Array<T> that contains elements from the input sequence that satisfy the condition.
         */
        where(predicate: (item: T, index: number) => boolean): T[];
        whereAsync(predicate: (item: T) => Promise<boolean>): Promise<T[]>;
        /**
         * Returns a specified number of contiguous elements from the start of a sequence.
         * @param count - The number of elements to return.
         * @returns Array<T> - An Array<T> that contains the specified number of elements from the start of the input sequence.
         */
        take(count: number): T[];
        /**
         * Returns a new array that contains the last count elements from source.
         * @param count - The number of elements to take from the end of the collection.
         * @returns Array<T> - A new array that contains the last count elements from source.
         */
        takeLast(count: number): T[];
        /**
         * Returns elements from an array as long as a specified condition is true.
         * @param predicate - A function to test each element for a condition.
         * @returns Array<T> - An new array that contains the elements from the input sequence that occur before the element at which the test no longer passes.
         */
        takeWhile(predicate: (item: T, index: number) => boolean): T[];
        /**
         * Creates a dictionary with type Map<TKey,TValue[]> from an Array<T> according to a specified key selector function, and an element selector function.
         * @param keySelector - An optional function to extract a key from each element.
         * @param elementSelector - An optional transform function to produce a result element value from each element.
         * @returns Map<TKey, TElement[]> - A dictionary with type Map<TKey,TValue[]> that contains keys and values. The values within each group are in the same order as in the source.
         */
        toDictionary<TKey = T, TElement = T>(keySelector?: ((item: T, index: number) => TKey) | null, elementSelector?: ((item: T, index: number) => TElement) | null): Map<TKey, TElement[]>;
        /**
         * Creates a hashset with type Set<TKey> from an Array<T> according to a specified key selector function.
         * @param keySelector - An optional function to extract a key from each element (optional).
         * @returns Set<TKey> - A hashset with type Set<TKey> that contains values of type TSource selected from the input sequence.
         */
        toHashSet<TKey = T>(keySelector?: ((item: T, index: number) => TKey) | null): Set<TKey>;
        /**
         * Returns the only element of a sequence that satisfies a specified condition, and throws an exception if more than one such element exists.
         * @param predicate - A function to test an element for a condition.
         * @param defaultValue - The default value to return if the sequence is empty.
         * @returns T - The single element of the input sequence that satisfies the condition, or defaultValue if no such element is found.
         */
        single(predicate?: ((item: T) => boolean) | null, defaultValue?: T | null): T;
        /**
         * Returns the only element of a sequence that satisfies a specified condition, or a specified default value if no such element exists; this method throws an exception if more than one element satisfies the condition.
         * @param predicate - A function to test an element for a condition.
         * @param defaultValue - The default value to return if the sequence is empty.
         * @returns T - The single element of the input sequence that satisfies the condition, or defaultValue if no such element is found.
         */
        singleOrDefault(predicate?: ((item: T) => boolean) | null, defaultValue?: T | null): T | null;
        /**
         * Bypasses a specified number of elements in a sequence and then returns the remaining elements.
         * @param count - A function to test each element for a condition.
         * @returns Array<T> - An Array<T> that contains the elements that occur after the specified index in the input sequence.
         */
        skip(count: number): T[];
        /**
         * Returns a new enumerable collection that contains the elements from source with the last count elements of the source collection omitted.
         * @param count - The number of elements to omit from the end of the collection.
         * @returns Array<T> - An Array<T> that contains the elements from source minus count elements from the end of the collection.
         */
        skipLast(count: number): T[];
        /**
         * Bypasses elements in a sequence as long as a specified condition is true and then returns the remaining elements.
         * @param predicate - A function to test each element (and its index) if the element is for a condition.
         * @returns Array<T> - An Array<T> that contains the elements from the input sequence starting at the first element in the linear series that does not pass the test specified by the predicate.
         */
        skipWhile(predicate: (item: T, index: number) => boolean): T[];
        /**
         * Projects each element of a sequence into a new form by incorporating the element's index.
         * @param selector - A transform function to apply to each source element; the second parameter of the function represents the index of the source element.
         * @returns Array<TResult> - An Array<TResult> whose elements are the result of invoking the transform function on each element of source.
         */
        select<TResult>(selector: (item: T, index: number) => TResult): TResult[];
        /**
         * Projects each element of a sequence to an Array<T> and flattens the resulting sequences into one sequence.
         * @param collectionSelector - A transform function to apply to each element of the input sequence.
         * @returns Array<TOut> - An Array<TOut> whose elements are the result of invoking the one-to-many transform function collectionSelector on each element of source and then mapping each of those sequence elements and their corresponding source element to a result element.
         */
        selectMany<TOut>(collectionSelector: (item: T) => TOut[]): TOut[];
        /**
         * Groups the elements of a sequence according to a key selector function. The keys are compared by using a comparer and each group's elements are projected by using a specified function.
         * @param keySelector - An optional function to extract the key for each element.
         * @param elementSelector - An optional function to map each source element to an element in the result grouped element.
         * @returns Array<T> - An array of grouped objects of type TElement.
         */
        groupBy<TKey = T, TElement = TKey>(keySelector?: ((item: T, index: number) => TKey) | null, elementSelector?: ((item: T, index: number) => TElement) | null): TElement[][];
        /**
         * Removes the first occurrence of a specific object from the Array<T>.
         * @param item - The object(s) to remove from the Array<T>. The value can be null for reference types.
         * @returns boolean - true if item is successfully removed; otherwise, false. This method also returns false if item was not found in the Array<T>.
         */
        remove(item: T | T[] | ((item: T, index: number) => boolean)): void;
        /**
         * Removes the element at the specified index of the Array<T>.
         * @param index - The zero-based index of the element to remove.
         * @exception ArgumentOutOfRangeException - index is less than 0 -or- index is equal to or greater than Count.
         */
        removeAt(index: number): void;
        /**
         * Returns the maximum value in a sequence of values.
         * @param keySelector - A function to extract the key for each element.
         * @returns T - The maximum value in the sequence.
         */
        max(keySelector: ((item: T) => number) | null): T;
        maxValue(keySelector: (item: T) => number): number;
        /**
         * Returns the minimum value in a sequence of values.
         * @param keySelector - A function to extract the key for each element.
         * @returns T - The minimum value in the sequence.
         */
        min(keySelector: ((item: T) => number) | null): T;
        minValue(keySelector: (item: T) => number): number;
        /**
         * Computes the sum of a sequence of numeric values.
         * @param selector - A transform function to apply to each element.
         * @returns number - The sum of the values in the sequence.
         */
        sum(selector?: ((item: T) => number | null | undefined) | null): number;
        /**
         * Returns the number of elements in a sequence.
         * @param predicate - A function to test each element for a condition.
         * @returns number - The number of elements in the input sequence if the predicate is not specified or, otherwise, the number of elements source that passes the test, specified by the predicate.
         */
        count(predicate?: ((item: T, index: number) => boolean) | null): number;
        /**
         * Determines whether a sequence contains a specified element by using the default equality comparer.
         * @param value - The value to locate in the sequence.
         * @returns boolean - true if the source sequence contains an element that has the specified value; otherwise, false.
         */
        contains(value: T): boolean;
        /**
         * Splits the elements of a sequence into chunks of size at most size.
         * @param size - The maximum size of each chunk.
         * @returns Array<T>[] - An Array<T> that contains the elements the input sequence split into chunks of size size.
         */
        chunk(size: number): T[][];
        /**
         * Splits the elements of a sequence into the specified count of chunks.
         * @param count - The count of chunks.
         * @returns Array<T>[] - An Array<T> that contains the elements of the input sequence is split into the specified count of chunks.
         */
        split(count: number): T[][];
        /**
         * Returns distinct elements from a sequence.
         * @param predicate - A predicate function to get comparable value.
         * @returns Array<T> - An Array<T> that contains distinct elements from the source sequence.
         */
        distinct(predicate?: ((item: T) => any) | null): T[];
        /**
         * Sorts the array in descending order.
         * @param keySelector1 - A function to extract the key for each element (the highest priority 0).
         * @param keySelector2 - A function to extract the key for each element (the priority 1).
         * @param keySelector3 - A function to extract the key for each element (the priority 2).
         * @param keySelector4 - A function to extract the key for each element (the priority 3).
         * @param keySelector5 - A function to extract the key for each element (the priority 4).
         * @param keySelector6 - A function to extract the key for each element (the priority lowest priority 5).
         */
        sortBy<TKey1, TKey2, TKey3, TKey4, TKey5, TKey6>(keySelector1?: ((item: T) => TKey1) | null, keySelector2?: ((item: T) => TKey2) | null, keySelector3?: ((item: T) => TKey3) | null, keySelector4?: ((item: T) => TKey4) | null, keySelector5?: ((item: T) => TKey5) | null, keySelector6?: ((item: T) => TKey6) | null): void;
        /**
         * Sorts the array in descending order.
         * @param keySelector1 - A function to extract the key for each element (the highest priority 0).
         * @param keySelector2 - A function to extract the key for each element (the priority 1).
         * @param keySelector3 - A function to extract the key for each element (the priority 2).
         * @param keySelector4 - A function to extract the key for each element (the priority 3).
         * @param keySelector5 - A function to extract the key for each element (the priority 4).
         * @param keySelector6 - A function to extract the key for each element (the priority lowest priority 5).
         */
        sortByDescending<TKey1, TKey2, TKey3, TKey4, TKey5, TKey6>(keySelector1?: ((item: T) => TKey1) | null, keySelector2?: ((item: T) => TKey2) | null, keySelector3?: ((item: T) => TKey3) | null, keySelector4?: ((item: T) => TKey4) | null, keySelector5?: ((item: T) => TKey5) | null, keySelector6?: ((item: T) => TKey6) | null): void;
        forEachAsync(predicate: (item: T) => Promise<void>): Promise<void>;
        /**
         * Produces the set difference of two sequences (the elements from the source collection not existing in the excepted collection).
         * @param except - An Array<T> whose elements that also occur in the source sequence will cause those elements to be removed from the returned sequence.
         * @param comparer - A function to compare values.
         * @returns T[] - An Array<T> sequence that contains the set difference of the elements of two sequences.
         */
        except(except: readonly T[], comparer?: ((x: T, y: T) => boolean) | null): T[];
        /**
         * Returns the first element of a sequence, or a default value if no element is found, or throw error if no default element is not specified.
         * @param predicate - A function to test each element for a condition.
         * @param defaultValue - The default value to return if the sequence is empty.
         * @returns T - defaultValue if source is empty or if no element passes the test specified by predicate; otherwise, the first element in source that passes the test specified by predicate.
         */
        first(predicate?: ((item: T) => boolean) | null, defaultValue?: T | null): T;
        /**
         * Returns the first element of a sequence, or a default value if no element is found.
         * @param predicate - A function to test each element for a condition.
         * @param defaultValue - The default value to return if the sequence is empty.
         * @returns T - defaultValue if source is empty or if no element passes the test specified by predicate; otherwise, the first element in source that passes the test specified by predicate.
         */
        firstOrDefault(predicate?: ((item: T) => boolean) | null, defaultValue?: T | null): T | null;
        /**
         * Returns the last element of a sequence, or a default value if no element is found, or throw error if no default element is not specified.
         * @param predicate - A function to test each element for a condition.
         * @param defaultValue - The default value to return if the sequence is empty.
         * @returns T - defaultValue if source is empty or if no element passes the test specified by predicate; otherwise, the last element in source that passes the test specified by predicate.
         */
        last(predicate?: ((item: T) => boolean) | null, defaultValue?: T | null): T;
        /**
         * Returns the last element of a sequence, or a specified default value if the sequence contains no elements.
         * @param predicate - A function to test each element for a condition.
         * @param defaultValue - The default value to return if the sequence is empty.
         * @returns T - defaultValue if source is empty or if no element passes the test specified by predicate; otherwise, the last element in source that passes the test specified by predicate.
         */
        lastOrDefault(predicate?: ((item: T) => boolean) | null, defaultValue?: T | null): T | null;
        /**
         * Generates a sequence that contains one repeated value.
         * @param element - The value to be repeated.
         * @param count - The number of times to repeat the value in the generated sequence.
         * @returns T[] - An Array<T> that contains a repeated value.
         */
        repeat(element: T, count: number): T[];
        /**
         * Inserts an element into the Array<T> at the specified index.
         * @param item - The object to insert.
         * @param index - The zero-based index at which item should be inserted.
         */
        insert(item: T | readonly T[], index?: number | null): void;
    }
}
export declare const ArrayExtensions: () => void;
